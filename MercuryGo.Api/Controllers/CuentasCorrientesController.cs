using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MercuryGo.Api.Auth;
using MercuryGo.Api.Data;
using MercuryGo.Api.Domain.Entities;
using MercuryGo.Api.Services;

namespace MercuryGo.Api.Controllers;

[ApiController]
[Authorize(Roles = $"{RolCodigos.Admin},{RolCodigos.Operario},{RolCodigos.Chofer},{RolCodigos.Cliente}")]
[Route("api/pedidos")]
public sealed class CuentasCorrientesController(
    MercuryGoDbContext db,
    IWebHostEnvironment env,
    MercuryGo.Api.Services.IdempotenciaService idempotenciaService) : PedidosControllerBase(db)
{
    // Registro de cobro en destino por parte del CHOFER (Efectivo o Transferencia / CU-13 / Decisión 9)
    [Authorize(Roles = $"{RolCodigos.Admin},{RolCodigos.Chofer}")]
    [HttpPost("{id:long}/cobro-chofer")]
    public async Task<IActionResult> RegistrarCobroChofer(
        long id,
        [FromBody] CobroChoferRequest request,
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey,
        CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(idempotencyKey))
        {
            var op = await idempotenciaService.ObtenerOperacionAsync(idempotencyKey, "COBRO_CHOFER", ct);
            if (op?.ResultadoJson != null)
            {
                return Content(op.ResultadoJson, "application/json");
            }
        }

        if (request.Monto <= 0)
            return BadRequest(new { codigo = "monto_invalido", mensaje = "El monto cobrado debe ser mayor a cero." });

        var pedido = await db.Pedidos.FirstOrDefaultAsync(p => p.Id == id && p.Activo, ct);
        if (pedido is null)
            return NotFound(new { codigo = "pedido_no_encontrado", mensaje = "El pedido no existe o fue cancelado." });

        // Solo se cobra una orden en reparto. El Admin puede registrar el cobro
        // después de la entrega como ajuste; el Chofer, no.
        var esAdmin = User.IsInRole(RolCodigos.Admin);
        var estadosCobrables = new[] { EstadoPedido.Despachado, EstadoPedido.EnPuerta, EstadoPedido.Entregado };

        if (!estadosCobrables.Contains(pedido.Estado))
            return BadRequest(new { codigo = "estado_invalido", mensaje = $"No se puede registrar un cobro sobre un pedido en estado '{pedido.Estado}'." });

        // Un chofer solo cobra los pedidos que tiene asignados.
        if (!esAdmin && (!pedido.ChoferId.HasValue || pedido.ChoferId.Value != User.UsuarioId()))
            return StatusCode(403, new { codigo = "chofer_no_asignado", mensaje = "El pedido está asignado a otro chofer." });

        CobroPedido nuevoCobro;
        var esRelacional = db.Database.IsRelational();
        await using var tx = esRelacional ? await db.Database.BeginTransactionAsync(ct) : null;
        try
        {
            // El acumulado de cobros no puede superar el total de la orden.
            var totalPedido = await db.DetallesPedido
                .Where(d => d.PedidoId == pedido.Id)
                .SumAsync(d => (decimal?)(d.Cantidad * d.PrecioUnitario), ct) ?? 0m;
            var yaCobrado = await db.CobrosPedido
                .Where(c => c.PedidoId == pedido.Id)
                .SumAsync(c => (decimal?)c.Monto, ct) ?? 0m;

            const decimal tolerancia = 0.01m;
            if (yaCobrado + request.Monto > totalPedido + tolerancia)
            {
                return BadRequest(new
                {
                    codigo = "monto_excede_total",
                    mensaje = $"El cobro supera el saldo de la orden. Total: ${totalPedido:N2}, ya cobrado: ${yaCobrado:N2}."
                });
            }

            var metodo = string.IsNullOrWhiteSpace(request.Metodo) ? "Efectivo" : request.Metodo.Trim();
            var ocurridoEn = request.OcurridoEn ?? DateTime.UtcNow;

            // 1. Guardar en tabla relacional estructurada
            nuevoCobro = new CobroPedido
            {
                PedidoId = pedido.Id,
                Monto = request.Monto,
                Metodo = metodo,
                RegistradoPorId = User.UsuarioId(),
                OcurridoEn = ocurridoEn,
                RecibidoEn = DateTime.UtcNow
            };
            db.CobrosPedido.Add(nuevoCobro);

            // 2. Preservar etiqueta en Observaciones para total retrocompatibilidad
            var logCobro = $"[Cobro Chofer: ${request.Monto:N2} ({metodo})]";
            var obs = pedido.Observaciones ?? string.Empty;
            pedido.Observaciones = string.IsNullOrWhiteSpace(obs) ? logCobro : $"{obs} · {logCobro}";
            pedido.ActualizadoEn = DateTime.UtcNow;

            await db.SaveChangesAsync(ct);
            if (tx != null)
            {
                await tx.CommitAsync(ct);
            }
        }
        catch
        {
            if (tx != null)
            {
                await tx.RollbackAsync(ct);
            }
            throw;
        }

        var respuestaObj = new
        {
            mensaje = $"Cobro de ${request.Monto:N2} registrado exitosamente.",
            cobro_id = nuevoCobro.Id,
            monto = nuevoCobro.Monto,
            metodo = nuevoCobro.Metodo,
            observaciones = pedido.Observaciones
        };

        var respuestaJson = System.Text.Json.JsonSerializer.Serialize(respuestaObj);
        if (!string.IsNullOrWhiteSpace(idempotencyKey))
        {
            await idempotenciaService.RegistrarOperacionAsync(
                idempotencyKey,
                "COBRO_CHOFER",
                User.UsuarioId(),
                pedido.Id,
                respuestaJson,
                ct
            );
        }

        return Ok(respuestaObj);
    }

    // Resumen de Cuenta Corriente y Deuda para el CLIENTE autenticado
    [Authorize(Roles = $"{RolCodigos.Admin},{RolCodigos.Cliente}")]
    [HttpGet("cuenta-corriente")]
    public async Task<IActionResult> ObtenerCuentaCorriente(CancellationToken ct)
    {
        var cliente = await ObtenerClienteDeUsuarioAutenticadoAsync(ct);
        if (cliente is null)
            return NotFound(new { codigo = "cliente_no_vinculado", mensaje = "Tu usuario no está vinculado a un cliente comercial." });

        var pedidosCliente = await db.Pedidos
            .AsNoTracking()
            .Where(p => p.ClienteId == cliente.Id && p.Activo && p.Estado != EstadoPedido.Cancelado)
            .Select(p => new ResumenPedidoContable(
                p.Id,
                p.Numero,
                p.ClienteId,
                p.Estado,
                p.FechaPedido,
                db.DetallesPedido.Where(d => d.PedidoId == p.Id).Sum(d => (decimal?)d.Cantidad * d.PrecioUnitario) ?? 0m,
                db.CobrosPedido.Where(c => c.PedidoId == p.Id).Sum(c => (decimal?)c.Monto) ?? 0m,
                p.ComprobanteEstado,
                p.ComprobanteUrl,
                p.Observaciones
            ))
            .ToListAsync(ct);

        var pagosCuenta = await db.PagosCuentaCorriente
            .AsNoTracking()
            .Where(p => p.ClienteId == cliente.Id && p.Activo)
            .OrderByDescending(p => p.CreadoEn)
            .ToListAsync(ct);

        var totalFacturado = pedidosCliente.Sum(p => p.Total);
        var totalPagado = pagosCuenta.Where(p => p.Estado == "Aprobado").Sum(p => p.Monto);
        var comprobantesPendientes = pagosCuenta.Count(p => p.Estado == "Pendiente");

        foreach (var p in pedidosCliente)
        {
            var (pagadoPedido, pendienteUrl) = EvaluarEstadoContablePedido(p);
            totalPagado += pagadoPedido;
            if (pendienteUrl is not null) comprobantesPendientes++;
        }

        var saldoDeudor = Math.Max(0m, totalFacturado - totalPagado);

        return Ok(new
        {
            cliente_id = cliente.Id,
            razon_social = cliente.RazonSocial,
            cuit = cliente.Cuit,
            total_facturado = totalFacturado,
            total_pagado = totalPagado,
            saldo_deudor = saldoDeudor,
            comprobantes_pendientes = comprobantesPendientes,
            pedidos_pendientes = pedidosCliente.Count(p => p.Estado != EstadoPedido.Entregado),
            pagos_cuenta_corriente = pagosCuenta.Select(p => new
            {
                p.Id,
                p.Monto,
                p.Estado,
                p.ComprobanteUrl,
                p.Observaciones,
                p.MotivoRechazo,
                p.CreadoEn
            })
        });
    }

    // Listado consolidado de saldos comerciales y comprobantes a auditar (Exclusivo ADMIN)
    [Authorize(Roles = RolCodigos.Admin)]
    [HttpGet("cuentas-corrientes-clientes")]
    public async Task<IActionResult> ListarCuentasCorrientesClientes(CancellationToken ct)
    {
        var clientes = await db.Clientes
            .AsNoTracking()
            .Where(c => c.Activo)
            .OrderBy(c => c.RazonSocial)
            .ToListAsync(ct);

        var pedidos = await db.Pedidos
            .AsNoTracking()
            .Where(p => p.Activo && p.Estado != EstadoPedido.Cancelado)
            .Select(p => new ResumenPedidoContable(
                p.Id,
                p.Numero,
                p.ClienteId,
                p.Estado,
                p.FechaPedido,
                db.DetallesPedido.Where(d => d.PedidoId == p.Id).Sum(d => (decimal?)d.Cantidad * d.PrecioUnitario) ?? 0m,
                db.CobrosPedido.Where(c => c.PedidoId == p.Id).Sum(c => (decimal?)c.Monto) ?? 0m,
                p.ComprobanteEstado,
                p.ComprobanteUrl,
                p.Observaciones
            ))
            .ToListAsync(ct);

        var todosPagosCuenta = await db.PagosCuentaCorriente
            .AsNoTracking()
            .Where(p => p.Activo)
            .OrderByDescending(p => p.CreadoEn)
            .ToListAsync(ct);

        var pedidosPorCliente = pedidos.ToLookup(p => p.ClienteId);
        var pagosPorCliente = todosPagosCuenta.ToLookup(p => p.ClienteId);

        var resumenPorCliente = clientes.Select(c =>
        {
            var pedidosC = pedidosPorCliente[c.Id];
            var pagosC = pagosPorCliente[c.Id];
            var totalFacturado = pedidosC.Sum(p => p.Total);
            var totalPagado = pagosC.Where(p => p.Estado == "Aprobado").Sum(p => p.Monto);
            var pendientesRevision = pagosC.Count(p => p.Estado == "Pendiente");
            var comprobantesPendientes = new List<object>();

            foreach (var pc in pagosC.Where(p => p.Estado == "Pendiente"))
            {
                comprobantesPendientes.Add(new
                {
                    pago_cuenta_id = pc.Id,
                    cliente_id = pc.ClienteId,
                    monto = pc.Monto,
                    fecha = pc.CreadoEn,
                    comprobante_url = pc.ComprobanteUrl,
                    observaciones = pc.Observaciones,
                    tipo = "cuenta_corriente"
                });
            }

            foreach (var p in pedidosC)
            {
                var (pagadoPedido, pendienteUrl) = EvaluarEstadoContablePedido(p);
                totalPagado += pagadoPedido;
                if (pendienteUrl is not null)
                {
                    pendientesRevision++;
                    comprobantesPendientes.Add(new
                    {
                        pedido_id = p.Id,
                        pedido_numero = p.Numero,
                        fecha = p.FechaPedido,
                        total = p.Total,
                        comprobante_url = pendienteUrl == string.Empty ? null : pendienteUrl,
                        observaciones = p.Observaciones
                    });
                }
            }

            var saldoDeudor = Math.Max(0m, totalFacturado - totalPagado);

            return new
            {
                cliente_id = c.Id,
                razon_social = c.RazonSocial,
                cuit = c.Cuit,
                telefono = c.Telefono,
                email = c.Email,
                localidad = c.Localidad,
                total_facturado = totalFacturado,
                total_pagado = totalPagado,
                saldo_deudor = saldoDeudor,
                comprobantes_pendientes_conteo = pendientesRevision,
                comprobantes_pendientes = comprobantesPendientes
            };
        }).ToList();

        var deudaTotalGeneral = resumenPorCliente.Sum(x => x.saldo_deudor);
        var clientesConDeuda = resumenPorCliente.Count(x => x.saldo_deudor > 0);
        var totalPendientesRevision = resumenPorCliente.Sum(x => x.comprobantes_pendientes_conteo);

        return Ok(new
        {
            metricas = new
            {
                deuda_total = deudaTotalGeneral,
                clientes_con_deuda = clientesConDeuda,
                total_comprobantes_pendientes = totalPendientesRevision
            },
            clientes = resumenPorCliente
        });
    }

    // Aprobación de comprobante bancario por el ADMIN (acredita el pago y reduce la deuda con el monto verificado / CU-15)
    [Authorize(Roles = RolCodigos.Admin)]
    [HttpPut("{id:long}/aprobar-comprobante")]
    public async Task<IActionResult> AprobarComprobante(long id, [FromBody] AprobarComprobanteRequest? request, CancellationToken ct)
    {
        var pedido = await db.Pedidos.FirstOrDefaultAsync(p => p.Id == id && p.Activo, ct);
        if (pedido is null)
            return NotFound(new { codigo = "pedido_no_encontrado", mensaje = "El pedido no existe o fue cancelado." });

        var totalPedido = await db.DetallesPedido
            .Where(d => d.PedidoId == id)
            .SumAsync(d => (decimal?)d.Cantidad * d.PrecioUnitario, ct) ?? 0m;

        var montoAprobado = request?.Monto.HasValue == true && request.Monto.Value > 0
            ? request.Monto.Value
            : totalPedido;

        var obs = pedido.Observaciones ?? string.Empty;
        var match = System.Text.RegularExpressions.Regex.Match(obs, @"/uploads/comprobantes/[^\s\]]+");
        var ruta = match.Success ? match.Value : (pedido.ComprobanteUrl ?? string.Empty);

        // Actualizar campos estructurados (Fase 1 / Sección 4)
        pedido.ComprobanteEstado = "Aprobado";
        pedido.ComprobanteUrl = string.IsNullOrWhiteSpace(ruta) ? null : ruta;
        pedido.AuditadoPorId = User.UsuarioId();
        pedido.AuditadoEn = DateTime.UtcNow;

        // Limpiar cualquier etiqueta previa de comprobante
        var obsLimpia = System.Text.RegularExpressions.Regex.Replace(obs, @"\s*·?\s*\[Comprobante de pago:[^\]]+\]", string.Empty).Trim();
        var nuevaEtiqueta = $"[Comprobante de pago: APROBADO · ${montoAprobado:N2} · {DateTime.UtcNow:dd/MM/yyyy HH:mm} · {ruta}]";

        pedido.Observaciones = string.IsNullOrWhiteSpace(obsLimpia)
            ? nuevaEtiqueta
            : $"{obsLimpia} · {nuevaEtiqueta}";

        pedido.ActualizadoEn = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);

        return Ok(new
        {
            mensaje = $"Comprobante de la orden {pedido.Numero} aprobado por ${montoAprobado:N2}. Pago acreditado exitosamente.",
            pedido_id = pedido.Id,
            comprobante_estado = pedido.ComprobanteEstado,
            monto_acreditado = montoAprobado,
            observaciones = pedido.Observaciones
        });
    }

    // Rechazo de comprobante bancario por el ADMIN (no descuenta y almacena motivo / CU-15)
    [Authorize(Roles = RolCodigos.Admin)]
    [HttpPut("{id:long}/rechazar-comprobante")]
    public async Task<IActionResult> RechazarComprobante(long id, [FromBody] RechazarComprobanteRequest request, CancellationToken ct)
    {
        var pedido = await db.Pedidos.FirstOrDefaultAsync(p => p.Id == id && p.Activo, ct);
        if (pedido is null)
            return NotFound(new { codigo = "pedido_no_encontrado", mensaje = "El pedido no existe o fue cancelado." });

        var obs = pedido.Observaciones ?? string.Empty;
        var match = System.Text.RegularExpressions.Regex.Match(obs, @"/uploads/comprobantes/[^\s\]]+");
        var ruta = match.Success ? match.Value : (pedido.ComprobanteUrl ?? string.Empty);

        var motivo = string.IsNullOrWhiteSpace(request?.Motivo) ? "No acreditado en cuenta bancaria" : request.Motivo.Trim();

        // Actualizar campos estructurados
        pedido.ComprobanteEstado = "Rechazado";
        pedido.ComprobanteUrl = string.IsNullOrWhiteSpace(ruta) ? null : ruta;
        pedido.AuditadoPorId = User.UsuarioId();
        pedido.AuditadoEn = DateTime.UtcNow;

        var obsLimpia = System.Text.RegularExpressions.Regex.Replace(obs, @"\s*·?\s*\[Comprobante de pago:[^\]]+\]", string.Empty).Trim();
        var nuevaEtiqueta = $"[Comprobante de pago: RECHAZADO - Motivo: {motivo} · {ruta}]";

        pedido.Observaciones = string.IsNullOrWhiteSpace(obsLimpia)
            ? nuevaEtiqueta
            : $"{obsLimpia} · {nuevaEtiqueta}";

        pedido.ActualizadoEn = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);

        return Ok(new
        {
            mensaje = $"Comprobante de la orden {pedido.Numero} rechazado: {motivo}",
            pedido_id = pedido.Id,
            comprobante_estado = pedido.ComprobanteEstado,
            observaciones = pedido.Observaciones
        });
    }

    // Subir pago directo a Cuenta Corriente con comprobante (D1)
    [Authorize(Roles = $"{RolCodigos.Admin},{RolCodigos.Cliente}")]
    [HttpPost("cuenta-corriente/pagos")]
    public async Task<IActionResult> RegistrarPagoCuentaCorriente(
        [FromForm] decimal monto,
        [FromForm] string? observaciones,
        IFormFile? archivo,
        CancellationToken ct)
    {
        if (monto <= 0)
            return BadRequest(new { codigo = "monto_invalido", mensaje = "El monto a abonar debe ser mayor a cero." });

        Cliente? cliente;
        if (User.EsCliente())
        {
            cliente = await ObtenerClienteDeUsuarioAutenticadoAsync(ct);
            if (cliente is null)
                return StatusCode(403, new { codigo = "cliente_no_vinculado", mensaje = "Tu usuario no está vinculado a un cliente comercial." });
        }
        else
        {
            var clienteIdForm = Request.Form["cliente_id"].ToString();
            if (!long.TryParse(clienteIdForm, out var cid) || cid <= 0)
                return BadRequest(new { codigo = "cliente_requerido", mensaje = "Debe especificar el ID del cliente destino." });

            cliente = await db.Clientes.FirstOrDefaultAsync(c => c.Id == cid && c.Activo, ct);
            if (cliente is null)
                return NotFound(new { codigo = "cliente_no_encontrado", mensaje = "El cliente no existe o está inactivo." });
        }

        string? comprobanteUrl = null;
        if (archivo is not null && archivo.Length > 0)
        {
            var extensionesValidas = new[] { ".pdf", ".jpg", ".jpeg", ".png", ".webp" };
            var extension = Path.GetExtension(archivo.FileName).ToLowerInvariant();
            if (archivo.Length > 5_000_000 || !extensionesValidas.Contains(extension))
                return BadRequest(new { codigo = "archivo_invalido", mensaje = "El comprobante debe ser un archivo PDF o imagen (JPG, PNG, WEBP) menor o igual a 5 MB." });

            var webRoot = env.WebRootPath ?? Path.Combine(env.ContentRootPath, "wwwroot");
            var carpeta = Path.Combine(webRoot, "uploads", "comprobantes");
            Directory.CreateDirectory(carpeta);

            var nombreArchivo = $"{Guid.NewGuid():N}{extension}";
            var rutaFisica = Path.Combine(carpeta, nombreArchivo);
            await using var stream = System.IO.File.Create(rutaFisica);
            await archivo.CopyToAsync(stream, ct);
            comprobanteUrl = $"/uploads/comprobantes/{nombreArchivo}";
        }

        var pago = new PagoCuentaCorriente
        {
            ClienteId = cliente.Id,
            Monto = monto,
            Estado = "Pendiente",
            ComprobanteUrl = comprobanteUrl,
            Observaciones = NormalizarTexto(observaciones),
            Activo = true,
            CreadoEn = DateTime.UtcNow
        };

        db.PagosCuentaCorriente.Add(pago);
        await db.SaveChangesAsync(ct);

        return Ok(new
        {
            pago_id = pago.Id,
            cliente_id = pago.ClienteId,
            monto = pago.Monto,
            estado = pago.Estado,
            comprobante_url = pago.ComprobanteUrl,
            mensaje = "Comprobante de pago a cuenta corriente registrado exitosamente. En proceso de verificación."
        });
    }

    // Listar pagos de cuenta corriente
    [Authorize(Roles = $"{RolCodigos.Admin},{RolCodigos.Cliente}")]
    [HttpGet("cuenta-corriente/pagos")]
    public async Task<IActionResult> ListarPagosCuentaCorriente(CancellationToken ct)
    {
        IQueryable<PagoCuentaCorriente> query = db.PagosCuentaCorriente.AsNoTracking().Where(p => p.Activo);

        if (User.EsCliente())
        {
            var cliente = await ObtenerClienteDeUsuarioAutenticadoAsync(ct);
            if (cliente is null)
                return StatusCode(403, new { codigo = "cliente_no_vinculado", mensaje = "Tu usuario no está vinculado a un cliente comercial." });

            query = query.Where(p => p.ClienteId == cliente.Id);
        }

        var pagos = await query
            .OrderByDescending(p => p.CreadoEn)
            .Select(p => new
            {
                p.Id,
                p.ClienteId,
                p.Monto,
                p.Estado,
                p.ComprobanteUrl,
                p.Observaciones,
                p.MotivoRechazo,
                p.AuditadoPorId,
                p.AuditadoEn,
                p.CreadoEn
            })
            .ToListAsync(ct);

        return Ok(new { pagos });
    }

    // Aprobar pago a cuenta corriente (Exclusivo ADMIN)
    [Authorize(Roles = RolCodigos.Admin)]
    [HttpPut("cuenta-corriente/pagos/{id:long}/aprobar")]
    public async Task<IActionResult> AprobarPagoCuentaCorriente(long id, [FromBody] AprobarComprobanteRequest? request, CancellationToken ct)
    {
        var pago = await db.PagosCuentaCorriente.FirstOrDefaultAsync(p => p.Id == id && p.Activo, ct);
        if (pago is null)
            return NotFound(new { codigo = "pago_no_encontrado", mensaje = "El pago a cuenta corriente no existe o fue dado de baja." });

        if (request?.Monto.HasValue == true && request.Monto.Value > 0)
        {
            pago.Monto = request.Monto.Value;
        }

        pago.Estado = "Aprobado";
        pago.AuditadoPorId = User.UsuarioId();
        pago.AuditadoEn = DateTime.UtcNow;
        pago.ActualizadoEn = DateTime.UtcNow;

        await db.SaveChangesAsync(ct);

        return Ok(new
        {
            mensaje = $"Pago a cuenta corriente #{pago.Id} aprobado exitosamente por ${pago.Monto:N2}.",
            pago_id = pago.Id,
            cliente_id = pago.ClienteId,
            monto = pago.Monto,
            estado = pago.Estado
        });
    }

    // Rechazar pago a cuenta corriente (Exclusivo ADMIN)
    [Authorize(Roles = RolCodigos.Admin)]
    [HttpPut("cuenta-corriente/pagos/{id:long}/rechazar")]
    public async Task<IActionResult> RechazarPagoCuentaCorriente(long id, [FromBody] RechazarComprobanteRequest request, CancellationToken ct)
    {
        var pago = await db.PagosCuentaCorriente.FirstOrDefaultAsync(p => p.Id == id && p.Activo, ct);
        if (pago is null)
            return NotFound(new { codigo = "pago_no_encontrado", mensaje = "El pago a cuenta corriente no existe o fue dado de baja." });

        var motivo = string.IsNullOrWhiteSpace(request?.Motivo) ? "Comprobante no acreditado en cuenta bancaria" : request.Motivo.Trim();

        pago.Estado = "Rechazado";
        pago.MotivoRechazo = motivo;
        pago.AuditadoPorId = User.UsuarioId();
        pago.AuditadoEn = DateTime.UtcNow;
        pago.ActualizadoEn = DateTime.UtcNow;

        await db.SaveChangesAsync(ct);

        return Ok(new
        {
            mensaje = $"Pago a cuenta corriente #{pago.Id} rechazado: {motivo}",
            pago_id = pago.Id,
            cliente_id = pago.ClienteId,
            estado = pago.Estado,
            motivo_rechazo = pago.MotivoRechazo
        });
    }


    // Descarga segura y autenticada de comprobante de pago de un pedido (P0-3)
    [HttpGet("{id:long}/comprobante-pago/archivo")]
    public async Task<IActionResult> ObtenerArchivoComprobantePago(long id, CancellationToken ct)
    {
        var pedido = await db.Pedidos.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id && p.Activo, ct);
        if (pedido is null)
            return NotFound(new { codigo = "pedido_no_encontrado", mensaje = "El remito/orden no existe o fue cancelado." });

        if (!await PuedeAccederAlPedidoAsync(pedido, ct))
            return StatusCode(StatusCodes.Status403Forbidden, new { codigo = "acceso_denegado", mensaje = "No tenés autorización para consultar los comprobantes de esta orden." });

        var rutaRelativa = pedido.ComprobanteUrl;
        if (string.IsNullOrWhiteSpace(rutaRelativa))
            return NotFound(new { codigo = "sin_comprobante", mensaje = "Esta orden no tiene ningún comprobante de pago adjunto." });

        var webRoot = env.WebRootPath ?? Path.Combine(env.ContentRootPath, "wwwroot");
        var carpetaPermitida = Path.GetFullPath(Path.Combine(webRoot, "uploads", "comprobantes"));
        var nombreSeguro = Path.GetFileName(rutaRelativa);
        var rutaFisica = Path.GetFullPath(Path.Combine(carpetaPermitida, nombreSeguro));
        if (!rutaFisica.StartsWith(carpetaPermitida + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            return BadRequest(new { codigo = "ruta_invalida", mensaje = "Ruta de comprobante inválida." });

        if (!System.IO.File.Exists(rutaFisica))
            return NotFound(new { codigo = "archivo_no_encontrado", mensaje = "El archivo físico del comprobante no existe en el servidor." });

        var extension = Path.GetExtension(rutaFisica).ToLowerInvariant();
        var contentType = extension switch
        {
            ".pdf" => "application/pdf",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            ".webp" => "image/webp",
            _ => "application/octet-stream"
        };

        return PhysicalFile(rutaFisica, contentType, Path.GetFileName(rutaFisica));
    }

    // Descarga segura y autenticada de comprobante de pago directo a cuenta corriente (P0-3)
    [Authorize(Roles = $"{RolCodigos.Admin},{RolCodigos.Cliente}")]
    [HttpGet("cuenta-corriente/pagos/{id:long}/archivo")]
    public async Task<IActionResult> ObtenerArchivoPagoCuentaCorriente(long id, CancellationToken ct)
    {
        var pago = await db.PagosCuentaCorriente.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id && p.Activo, ct);
        if (pago is null)
            return NotFound(new { codigo = "pago_no_encontrado", mensaje = "El pago a cuenta corriente no existe o fue dado de baja." });

        if (User.EsCliente())
        {
            var cliente = await ObtenerClienteDeUsuarioAutenticadoAsync(ct);
            if (cliente is null || cliente.Id != pago.ClienteId)
                return StatusCode(StatusCodes.Status403Forbidden, new { codigo = "acceso_denegado", mensaje = "No tenés autorización para ver este comprobante." });
        }

        if (string.IsNullOrWhiteSpace(pago.ComprobanteUrl))
            return NotFound(new { codigo = "sin_comprobante", mensaje = "Este abono no contiene un comprobante adjunto." });

        var webRoot = env.WebRootPath ?? Path.Combine(env.ContentRootPath, "wwwroot");
        var carpetaPermitida = Path.GetFullPath(Path.Combine(webRoot, "uploads", "comprobantes"));
        var nombreSeguro = Path.GetFileName(pago.ComprobanteUrl);
        var rutaFisica = Path.GetFullPath(Path.Combine(carpetaPermitida, nombreSeguro));
        if (!rutaFisica.StartsWith(carpetaPermitida + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            return BadRequest(new { codigo = "ruta_invalida", mensaje = "Ruta de comprobante inválida." });

        if (!System.IO.File.Exists(rutaFisica))
            return NotFound(new { codigo = "archivo_no_encontrado", mensaje = "El archivo físico del comprobante no existe en el servidor." });

        var extension = Path.GetExtension(rutaFisica).ToLowerInvariant();
        var contentType = extension switch
        {
            ".pdf" => "application/pdf",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            ".webp" => "image/webp",
            _ => "application/octet-stream"
        };

        return PhysicalFile(rutaFisica, contentType, Path.GetFileName(rutaFisica));
    }

    // Subida de comprobante de transferencia bancaria (PDF o Foto: JPG, PNG, WEBP hasta 5 MB)
    [HttpPost("{id:long}/comprobante-pago")]
    public async Task<IActionResult> SubirComprobantePago(long id, IFormFile? archivo, CancellationToken ct)
    {
        if (archivo is null || archivo.Length == 0)
            return BadRequest(new { codigo = "archivo_requerido", mensaje = "Debe adjuntar una foto o archivo PDF del comprobante de transferencia." });

        var extensionesValidas = new[] { ".pdf", ".jpg", ".jpeg", ".png", ".webp" };
        var extension = Path.GetExtension(archivo.FileName).ToLowerInvariant();
        if (archivo.Length > 5_000_000 || !extensionesValidas.Contains(extension))
            return BadRequest(new { codigo = "archivo_invalido", mensaje = "El comprobante debe ser un archivo PDF o imagen (JPG, PNG, WEBP) menor o igual a 5 MB." });

        var pedido = await db.Pedidos.FirstOrDefaultAsync(p => p.Id == id && p.Activo, ct);
        if (pedido is null)
            return NotFound(new { codigo = "pedido_no_encontrado", mensaje = "El pedido no existe o fue cancelado." });

        if (!await PuedeAccederAlPedidoAsync(pedido, ct))
            return StatusCode(StatusCodes.Status403Forbidden, new { codigo = "acceso_denegado", mensaje = "No tenés autorización para adjuntar comprobantes a esta orden." });

        var webRoot = env.WebRootPath ?? Path.Combine(env.ContentRootPath, "wwwroot");
        var carpetaComprobantes = Path.Combine(webRoot, "uploads", "comprobantes");
        Directory.CreateDirectory(carpetaComprobantes);

        var nombreArchivo = $"{Guid.NewGuid():N}{extension}";
        var rutaFisica = Path.Combine(carpetaComprobantes, nombreArchivo);
        await using var stream = System.IO.File.Create(rutaFisica);
        await archivo.CopyToAsync(stream, ct);

        // Guardamos en campos estructurados (CU-14 / Decisión 9)
        pedido.ComprobanteEstado = "Pendiente";
        pedido.ComprobanteUrl = $"/uploads/comprobantes/{nombreArchivo}";

        // Guardamos también la referencia en observaciones para total retrocompatibilidad
        var etiquetaComprobante = $"[Comprobante de pago: PENDIENTE · /uploads/comprobantes/{nombreArchivo}]";
        var obsActual = pedido.Observaciones ?? string.Empty;
        var obsLimpia = System.Text.RegularExpressions.Regex.Replace(obsActual, @"\s*·?\s*\[Comprobante de pago:[^\]]+\]", string.Empty).Trim();

        pedido.Observaciones = string.IsNullOrWhiteSpace(obsLimpia)
            ? etiquetaComprobante
            : $"{obsLimpia} · {etiquetaComprobante}";

        pedido.ActualizadoEn = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);

        return Ok(new
        {
            comprobante_url = pedido.ComprobanteUrl,
            comprobante_estado = pedido.ComprobanteEstado,
            mensaje = "Comprobante de pago adjuntado exitosamente a la orden."
        });
    }

    // Ajuste manual de cobro por parte del Administrador (Decisión 4)
    [Authorize(Roles = RolCodigos.Admin)]
    [HttpPut("cobros/{cobroId:long}/ajustar")]
    public async Task<IActionResult> AjustarCobro(long cobroId, [FromBody] AjustarCobroRequest request, CancellationToken ct)
    {
        var cobro = await db.CobrosPedido.FirstOrDefaultAsync(c => c.Id == cobroId, ct);
        if (cobro is null)
            return NotFound(new { codigo = "cobro_no_encontrado", mensaje = "El cobro no existe." });

        if (request.NuevoMonto < 0)
            return BadRequest(new { codigo = "monto_invalido", mensaje = "El monto ajustado no puede ser negativo." });

        cobro.Monto = request.NuevoMonto;
        if (!string.IsNullOrWhiteSpace(request.Metodo)) cobro.Metodo = request.Metodo.Trim();
        cobro.RecibidoEn = DateTime.UtcNow;

        await db.SaveChangesAsync(ct);

        return Ok(new
        {
            mensaje = $"Cobro ajustado a ${cobro.Monto:N2}.",
            cobro_id = cobro.Id,
            monto = cobro.Monto,
            metodo = cobro.Metodo
        });
    }

    private sealed record ResumenPedidoContable(
        long Id,
        string Numero,
        long ClienteId,
        EstadoPedido Estado,
        DateTime FechaPedido,
        decimal Total,
        decimal CobradoEstructurado,
        string? ComprobanteEstado,
        string? ComprobanteUrl,
        string? Observaciones);

    // Evalúa en un solo lugar el total abonado de un pedido (priorizando CobrosPedido estructurados)
    // y si tiene un comprobante pendiente de revisión por el Admin.
    private static (decimal TotalAbonado, string? ComprobantePendienteUrl) EvaluarEstadoContablePedido(ResumenPedidoContable p)
    {
        var obs = p.Observaciones ?? string.Empty;
        var abonado = p.CobradoEstructurado;

        if (p.ComprobanteEstado == "Aprobado" || obs.Contains("[Comprobante de pago: APROBADO"))
        {
            var matchMonto = System.Text.RegularExpressions.Regex.Match(obs, @"\[Comprobante de pago:\s*APROBADO\s*·\s*\$([\d\.,]+)");
            if (matchMonto.Success && decimal.TryParse(
                matchMonto.Groups[1].Value.Replace(".", string.Empty).Replace(",", "."),
                System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture,
                out var montoAprob))
            {
                abonado += montoAprob;
            }
            else if (abonado == 0m)
            {
                abonado = p.Total;
            }
        }
        else if (abonado == 0m && obs.Contains("[Cobro Chofer:"))
        {
            var matchCobro = System.Text.RegularExpressions.Regex.Match(obs, @"\[Cobro Chofer:\s*\$([\d\.,]+)");
            abonado = matchCobro.Success && decimal.TryParse(
                matchCobro.Groups[1].Value.Replace(".", string.Empty).Replace(",", "."),
                System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture,
                out var montoCobro)
                ? montoCobro
                : p.Total;
        }

        string? pendienteUrl = null;
        if (p.ComprobanteEstado == "Pendiente" || obs.Contains("[Comprobante de pago: PENDIENTE") || obs.Contains("[Comprobante de pago: /uploads/"))
        {
            if (!string.IsNullOrWhiteSpace(p.ComprobanteUrl))
            {
                pendienteUrl = p.ComprobanteUrl;
            }
            else
            {
                var match = System.Text.RegularExpressions.Regex.Match(obs, @"/uploads/comprobantes/[^\s\]]+");
                pendienteUrl = match.Success ? match.Value : string.Empty;
            }
        }

        return (Math.Min(p.Total, abonado), pendienteUrl);
    }
}