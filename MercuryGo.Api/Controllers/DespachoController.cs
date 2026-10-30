using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QRCoder;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using MercuryGo.Api.Auth;
using MercuryGo.Api.Data;
using MercuryGo.Api.Domain.Entities;
using MercuryGo.Api.Services;

namespace MercuryGo.Api.Controllers;

[ApiController]
[Authorize(Roles = $"{RolCodigos.Admin},{RolCodigos.Operario},{RolCodigos.Chofer},{RolCodigos.Cliente}")]
[Route("api/pedidos")]
public sealed class DespachoController(
    MercuryGoDbContext db,
    MercuryGo.Api.Services.IPedidoTransicionService transicionService,
    MercuryGo.Api.Services.IdempotenciaService idempotenciaService,
    MercuryGo.Api.Services.NotificacionService notificacionService,
    MercuryGo.Api.Services.ConflictoOfflineService conflictoOfflineService) : PedidosControllerBase(db, conflictoOfflineService)
{
    // Registro de picking/preparación de depósito (Operario o Admin)
    [Authorize(Roles = $"{RolCodigos.Admin},{RolCodigos.Operario}")]
    [HttpPut("{id:long}/picking")]
    public async Task<IActionResult> ActualizarPicking(long id, [FromBody] ActualizarPickingRequest request, CancellationToken ct)
    {
        if (request.Items is null || request.Items.Count == 0)
            return BadRequest(new { codigo = "items_requeridos", mensaje = "Debe enviar el estado de picking de los productos." });

        var pedido = await db.Pedidos.FirstOrDefaultAsync(p => p.Id == id && p.Activo, ct);
        if (pedido is null)
            return NotFound(new { codigo = "pedido_no_encontrado", mensaje = "El pedido no existe o fue cancelado." });

        if (pedido.Estado == EstadoPedido.Entregado)
            return BadRequest(new { codigo = "estado_invalido", mensaje = "No se puede realizar picking sobre un pedido finalizado." });

        // Decisión 16: si el pedido se canceló mientras el operario preparaba sin red,
        // el picking se guarda igual (es trabajo físico real), pero la orden sigue
        // Cancelada y no se tocan sus importes.
        var pedidoCancelado = pedido.Estado == EstadoPedido.Cancelado;

        var detalles = await (
            from d in db.DetallesPedido
            join prod in db.Productos on d.ProductoId equals prod.Id
            where d.PedidoId == id
            select new { d, ProductoNombre = prod.Nombre }
        ).ToListAsync(ct);

        var mapaRequest = request.Items.ToDictionary(i => i.ProductoId);
        var faltantes = new List<string>();
        var esCompleto = true;

        foreach (var item in detalles)
        {
            if (mapaRequest.TryGetValue(item.d.ProductoId, out var pick))
            {
                item.d.CantidadPreparada = Math.Max(0, pick.CantidadPreparada);
                var enFalta = string.Equals(pick.EstadoItem, "EnFalta", StringComparison.OrdinalIgnoreCase) || pick.CantidadPreparada < item.d.Cantidad;
                if (enFalta)
                {
                    esCompleto = false;
                    faltantes.Add($"{item.ProductoNombre} (prep. {pick.CantidadPreparada}/{item.d.Cantidad})");

                    // Decisión 5: si el picking queda incompleto, se ajusta la cantidad
                    // definitiva de la orden. Sobre una orden cancelada no se toca el
                    // importe: solo se registra lo que efectivamente se preparó.
                    var diferencia = item.d.Cantidad - item.d.CantidadPreparada;
                    if (!pedidoCancelado && diferencia > 0)
                    {
                        item.d.Cantidad = item.d.CantidadPreparada;
                    }
                }
            }
            else
            {
                esCompleto = false;
                faltantes.Add($"{item.ProductoNombre} (no verificado)");
            }
        }

        pedido.PickingResultado = esCompleto ? "Completo" : "Incompleto";
        pedido.PickingEn = DateTime.UtcNow;

        var logPicking = esCompleto
            ? "[Picking: COMPLETO]"
            : $"[Picking: INCOMPLETO - Faltantes: {string.Join(", ", faltantes)}]";

        var obsActual = pedido.Observaciones ?? string.Empty;
        var patronPicking = System.Text.RegularExpressions.Regex.Replace(obsActual, @"\s*·?\s*\[Picking:[^\]]+\]", string.Empty).Trim();

        pedido.Observaciones = string.IsNullOrWhiteSpace(patronPicking)
            ? logPicking
            : $"{patronPicking} · {logPicking}";

        if (pedido.Estado == EstadoPedido.Confirmado)
        {
            pedido.Estado = EstadoPedido.EnPreparacion;
        }

        pedido.ActualizadoEn = DateTime.UtcNow;

        // Aviso al Admin cuando falta mercadería: sin esto, el faltante solo se ve
        // si alguien abre la orden.
        if (request.EsOffline && pedidoCancelado)
        {
            // Decisión 16: el picking se guardó, pero sobre una orden que el Admin
            // ya había cancelado. Un solo aviso, que incluye los faltantes.
            await conflictoOfflineService!.AvisarPickingSobreCanceladoAsync(
                pedido, User.UsuarioId(), faltantes.Count > 0, request.EsOffline, ct);
        }
        else if (faltantes.Count > 0)
        {
            var detalleFaltantes = string.Join("; ", faltantes);
            if (detalleFaltantes.Length > 400)
                detalleFaltantes = detalleFaltantes[..400] + "…";

            await notificacionService.NotificarRolAsync(
                RolCodigos.Admin,
                $"⚠️ Picking incompleto ({pedido.Numero})",
                $"Faltantes: {detalleFaltantes}",
                "PEDIDO",
                pedido.Id,
                ct);
        }

        // Terminar el picking deja el pedido listo para salir: si ya tiene chofer,
        // se despacha en la misma acción (CU-11). Sin chofer queda en preparación.
        var despachado = false;
        var requiereChofer = false;

        if (pedido.Estado == EstadoPedido.EnPreparacion)
        {
            if (pedido.ChoferId.HasValue)
            {
                var contexto = new ContextoTransicion(
                    UsuarioId: User.UsuarioId(),
                    RolCodigo: User.RolCodigo() ?? string.Empty,
                    OcurridoEn: DateTime.UtcNow);

                var transicion = await transicionService.TransicionarAsync(
                    pedido, EstadoPedido.Despachado, contexto, ct);

                if (!transicion.Exito)
                    return BadRequest(new { codigo = transicion.CodigoError, mensaje = transicion.MensajeError });

                despachado = true;
            }
            else
            {
                requiereChofer = true;
            }
        }

        await db.SaveChangesAsync(ct);

        var mensaje = pedidoCancelado
            ? "Picking registrado sobre un pedido cancelado. La orden sigue cancelada."
            : despachado
                ? esCompleto
                    ? "Picking completo. Pedido despachado y asignado al chofer."
                    : "Picking con faltantes. Pedido ajustado y despachado con lo preparado."
                : requiereChofer
                    ? "Picking guardado. Asigná un chofer para despachar el pedido."
                    : esCompleto
                        ? "Picking completado exitosamente. Todo listo para despacho."
                        : "Picking registrado con faltantes. Pedido ajustado a lo preparado.";

        return Ok(new
        {
            completo = esCompleto,
            estado = pedido.Estado.ToString(),
            picking_resultado = pedido.PickingResultado,
            faltantes,
            observaciones = pedido.Observaciones,
            despachado,
            requiere_chofer = requiereChofer,
            mensaje
        });
    }


    // Comprobante oficial en PDF con QuestPDF y QR impreso al pie
    [HttpGet("{id:long}/comprobante")]
    public async Task<IActionResult> Comprobante(long id, CancellationToken ct)
    {
        var pedido = await (
            from p in db.Pedidos.AsNoTracking()
            join c in db.Clientes.AsNoTracking() on p.ClienteId equals c.Id
            where p.Id == id && p.Activo
            select new { p, c }
        ).FirstOrDefaultAsync(ct);

        if (pedido is null)
            return NotFound(new { codigo = "pedido_no_encontrado", mensaje = "El pedido no existe o fue cancelado." });

        if (!await PuedeAccederAlPedidoAsync(pedido.p, ct))
            return StatusCode(StatusCodes.Status403Forbidden, new { codigo = "acceso_denegado", mensaje = "No tenés autorización para descargar el remito de esta orden." });

        var detalles = await (
            from d in db.DetallesPedido.AsNoTracking()
            join prod in db.Productos.AsNoTracking() on d.ProductoId equals prod.Id
            where d.PedidoId == id
            orderby prod.Nombre
            select new DetallePedidoResponse(
                d.Id,
                prod.Id,
                prod.Nombre,
                d.Cantidad,
                d.PrecioUnitario,
                d.Cantidad * d.PrecioUnitario
            )
        ).ToListAsync(ct);

        var total = Math.Round(detalles.Sum(d => d.Subtotal), 2);

        var pdfBytes = GenerarComprobantePdf(pedido.p, pedido.c, detalles, total);
        return File(pdfBytes, "application/pdf", $"{pedido.p.Numero}.pdf");
    }

    private static byte[] GenerarComprobantePdf(Pedido p, Cliente c, List<DetallePedidoResponse> detalles, decimal total)
    {
        return Document.Create(doc =>
        {
            doc.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(36);
                page.DefaultTextStyle(x => x.FontSize(10));

                page.Header().Column(col =>
                {
                    col.Item().Row(r =>
                    {
                        r.RelativeItem().Column(cLeft =>
                        {
                            cLeft.Item().Text("MercuryGO").FontSize(18).Bold().FontColor(Colors.Blue.Darken2);
                            cLeft.Item().Text("Logística & Distribución B2B").FontSize(10).FontColor(Colors.Grey.Darken1);
                        });
                        r.ConstantItem(140).Column(cRight =>
                        {
                            cRight.Item().AlignRight().Text($"Orden: {p.Numero}").FontSize(12).Bold();
                            cRight.Item().AlignRight().Text($"Fecha: {p.FechaPedido:dd/MM/yyyy HH:mm}").FontSize(9);
                            cRight.Item().AlignRight().Text($"Estado: {p.Estado}").FontSize(9).Bold().FontColor(Colors.Green.Darken2);
                        });
                    });

                    col.Item().PaddingTop(10).LineHorizontal(1).LineColor(Colors.Grey.Lighten2);

                    col.Item().PaddingTop(10).Row(r =>
                    {
                        r.RelativeItem().Column(clienteCol =>
                        {
                            clienteCol.Item().Text($"Cliente: {c.RazonSocial}").Bold();
                            clienteCol.Item().Text($"CUIT: {c.Cuit}");
                            if (!string.IsNullOrWhiteSpace(c.Direccion))
                                clienteCol.Item().Text($"Entrega: {c.Direccion} ({c.Localidad})");
                            if (!string.IsNullOrWhiteSpace(c.Telefono))
                                clienteCol.Item().Text($"Tel: {c.Telefono}");
                        });
                    });

                    col.Item().PaddingVertical(10).LineHorizontal(1).LineColor(Colors.Grey.Lighten2);
                });

                page.Content().Column(col =>
                {
                    col.Item().Table(tabla =>
                    {
                        tabla.ColumnsDefinition(cols =>
                        {
                            cols.ConstantColumn(30);
                            cols.RelativeColumn(4);
                            cols.RelativeColumn(2);
                            cols.RelativeColumn(2);
                            cols.RelativeColumn(2);
                        });

                        tabla.Header(h =>
                        {
                            h.Cell().Element(CeldaEncabezado).Text("#").Bold();
                            h.Cell().Element(CeldaEncabezado).Text("Producto / Mercadería").Bold();
                            h.Cell().Element(CeldaEncabezado).AlignRight().Text("Cantidad").Bold();
                            h.Cell().Element(CeldaEncabezado).AlignRight().Text("P. Unit.").Bold();
                            h.Cell().Element(CeldaEncabezado).AlignRight().Text("Subtotal").Bold();
                        });

                        var idx = 1;
                        foreach (var d in detalles)
                        {
                            tabla.Cell().Element(CeldaCuerpo).Text($"{idx++}");
                            tabla.Cell().Element(CeldaCuerpo).Text(d.ProductoNombre);
                            tabla.Cell().Element(CeldaCuerpo).AlignRight().Text($"{d.Cantidad}");
                            tabla.Cell().Element(CeldaCuerpo).AlignRight().Text($"${d.PrecioUnitario:N2}");
                            tabla.Cell().Element(CeldaCuerpo).AlignRight().Text($"${d.Subtotal:N2}").Bold();
                        }
                    });

                    col.Item().PaddingTop(12).AlignRight().Text($"TOTAL: ${total:N2}").FontSize(14).Bold().FontColor(Colors.Blue.Darken3);

                    if (!string.IsNullOrWhiteSpace(p.Observaciones))
                    {
                        col.Item().PaddingTop(10).Background(Colors.Grey.Lighten4).Padding(8).Column(obs =>
                        {
                            obs.Item().Text("Observaciones de Entrega:").Bold().FontSize(9);
                            obs.Item().Text(p.Observaciones).FontSize(9);
                        });
                    }
                });

                page.Footer().AlignCenter().Column(f =>
                {
                    f.Item().AlignCenter().Width(75).Image(GenerarQrPedido(p.Numero));
                    f.Item().AlignCenter().Text(p.Numero).FontSize(8).Bold();
                    f.Item().AlignCenter().Text("Escanee este código desde la app móvil para consultar la orden.").FontSize(7).FontColor(Colors.Grey.Darken1);
                });
            });
        }).GeneratePdf();
    }


    // Listado de choferes activos para el selector de asignación (CU-20)
    [Authorize(Roles = $"{RolCodigos.Admin},{RolCodigos.Operario}")]
    [HttpGet("choferes")]
    public async Task<IActionResult> Choferes(CancellationToken ct)
    {
        var choferes = await (
            from u in db.Usuarios.AsNoTracking()
            join r in db.Roles.AsNoTracking() on u.RolId equals r.Id
            where u.Activo && r.Codigo == RolCodigos.Chofer
            orderby u.Nombre
            select new { id = u.Id, nombre = u.Nombre }
        ).ToListAsync(ct);

        return Ok(new { choferes });
    }

    // Asignación o reasignación de chofer (CU-20 / OPERARIO y ADMIN)
    [Authorize(Roles = $"{RolCodigos.Admin},{RolCodigos.Operario}")]
    [HttpPut("{id:long}/chofer")]
    public async Task<IActionResult> AsignarChofer(long id, [FromBody] AsignarChoferRequest request, CancellationToken ct)
    {
        var pedido = await db.Pedidos.FirstOrDefaultAsync(p => p.Id == id && p.Activo, ct);
        if (pedido is null)
            return NotFound(new { codigo = "pedido_no_encontrado", mensaje = "El pedido no existe o fue cancelado." });

        if (pedido.Estado == EstadoPedido.Entregado || pedido.Estado == EstadoPedido.Cancelado)
            return BadRequest(new { codigo = "estado_invalido", mensaje = "No se puede asignar chofer a un pedido finalizado o cancelado." });

        var chofer = await (
            from u in db.Usuarios
            join r in db.Roles on u.RolId equals r.Id
            where u.Id == request.ChoferId && u.Activo && r.Codigo == RolCodigos.Chofer
            select u
        ).FirstOrDefaultAsync(ct);

        if (chofer is null)
            return BadRequest(new { codigo = "chofer_invalido", mensaje = "El chofer seleccionado no existe, está inactivo o no tiene rol de chofer." });

        pedido.ChoferId = chofer.Id;
        pedido.ActualizadoEn = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);

        return Ok(new
        {
            mensaje = $"Chofer '{chofer.Nombre}' asignado correctamente a la orden {pedido.Numero}.",
            pedido_id = pedido.Id,
            chofer_id = pedido.ChoferId
        });
    }

    // Registro de entrega fallida (CU-21 / CHOFER y ADMIN)
    [Authorize(Roles = $"{RolCodigos.Admin},{RolCodigos.Chofer}")]
    [HttpPost("{id:long}/no-entregado")]
    public async Task<IActionResult> RegistrarEntregaFallida(
        long id,
        [FromBody] EntregaFallidaRequest request,
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey,
        CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(idempotencyKey))
        {
            var op = await idempotenciaService.ObtenerOperacionAsync(idempotencyKey, "ENTREGA_FALLIDA", ct);
            if (op?.ResultadoJson != null)
                return Content(op.ResultadoJson, "application/json");
        }

        if (string.IsNullOrWhiteSpace(request?.Motivo))
            return BadRequest(new { codigo = "motivo_requerido", mensaje = "Debe indicar el motivo de la entrega fallida." });

        if (!MercuryGo.Api.Utils.ValidadorEvidencia.EsEvidenciaValida(request.FotoUrl))
            return BadRequest(new { codigo = "evidencia_invalida", mensaje = "La foto adjunta tiene un formato o URL no permitido." });

        var pedido = await db.Pedidos.FirstOrDefaultAsync(p => p.Id == id && p.Activo, ct);
        if (pedido is null)
            return NotFound(new { codigo = "pedido_no_encontrado", mensaje = "El pedido no existe o fue cancelado." });

        var contexto = new MercuryGo.Api.Services.ContextoTransicion(
            UsuarioId: User.UsuarioId(),
            RolCodigo: User.RolCodigo() ?? string.Empty,
            Latitud: request.Latitud,
            Longitud: request.Longitud,
            EsSincronizacionOffline: request.EsOffline,
            OcurridoEn: request.OcurridoEn
        );

        var estadoAnterior = pedido.Estado;
        var resultado = await transicionService.TransicionarAsync(pedido, EstadoPedido.NoEntregado, contexto, ct);
        if (!resultado.Exito)
            return BadRequest(new { codigo = resultado.CodigoError, mensaje = resultado.MensajeError });

        await AvisarConflictosOfflineAsync(
            pedido, estadoAnterior, request.EsOffline, EstadoPedido.NoEntregado, ct);

        var intento = new IntentoEntrega
        {
            PedidoId = pedido.Id,
            ChoferId = User.UsuarioId(),
            Motivo = request.Motivo.Trim(),
            Nota = NormalizarTexto(request.Nota),
            FotoUrl = request.FotoUrl,
            OcurridoEn = request.OcurridoEn ?? DateTime.UtcNow
        };
        db.IntentosEntrega.Add(intento);

        await db.SaveChangesAsync(ct);

        var respuestaObj = new
        {
            mensaje = "Entrega fallida registrada. Pedido en estado NoEntregado.",
            estado = pedido.Estado.ToString(),
            intento_id = intento.Id,
            motivo = intento.Motivo
        };

        if (!string.IsNullOrWhiteSpace(idempotencyKey))
        {
            await idempotenciaService.RegistrarOperacionAsync(
                idempotencyKey,
                "ENTREGA_FALLIDA",
                User.UsuarioId(),
                pedido.Id,
                System.Text.Json.JsonSerializer.Serialize(respuestaObj),
                ct
            );
        }

        return Ok(respuestaObj);
    }

    // Reintento o reprogramación de entrega (CU-21 / ADMIN)
    [Authorize(Roles = RolCodigos.Admin)]
    [HttpPost("{id:long}/reintentar-entrega")]
    public async Task<IActionResult> ReintentarEntrega(
        long id,
        [FromBody] ReintentarEntregaRequest? request,
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey,
        CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(idempotencyKey))
        {
            var op = await idempotenciaService.ObtenerOperacionAsync(idempotencyKey, "REINTENTAR_ENTREGA", ct);
            if (op?.ResultadoJson != null)
                return Content(op.ResultadoJson, "application/json");
        }

        var pedido = await db.Pedidos.FirstOrDefaultAsync(p => p.Id == id && p.Activo, ct);
        if (pedido is null)
            return NotFound(new { codigo = "pedido_no_encontrado", mensaje = "El pedido no existe o fue cancelado." });

        if (pedido.Estado != EstadoPedido.NoEntregado)
            return BadRequest(new { codigo = "estado_invalido", mensaje = "Solo pedidos en estado NoEntregado admiten reintento de despacho." });

        if (request?.ChoferId.HasValue == true)
        {
            pedido.ChoferId = request.ChoferId.Value;
        }

        var contexto = new MercuryGo.Api.Services.ContextoTransicion(
            UsuarioId: User.UsuarioId(),
            RolCodigo: User.RolCodigo() ?? string.Empty
        );

        var resultado = await transicionService.TransicionarAsync(pedido, EstadoPedido.Despachado, contexto, ct);
        if (!resultado.Exito)
            return BadRequest(new { codigo = resultado.CodigoError, mensaje = resultado.MensajeError });

        await db.SaveChangesAsync(ct);

        return Ok(new
        {
            mensaje = $"Orden {pedido.Numero} reprogramada y despachada nuevamente.",
            estado = pedido.Estado.ToString(),
            chofer_id = pedido.ChoferId
        });
    }

    // Cierre de entrega parcial con ítems rechazados (CU-13 / Decisión 14)
    [Authorize(Roles = $"{RolCodigos.Admin},{RolCodigos.Chofer}")]
    [HttpPost("{id:long}/entrega-parcial")]
    public async Task<IActionResult> EntregaParcial(
        long id,
        [FromBody] EntregaParcialRequest request,
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey,
        CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(idempotencyKey))
        {
            var op = await idempotenciaService.ObtenerOperacionAsync(idempotencyKey, "ENTREGA_PARCIAL", ct);
            if (op?.ResultadoJson != null)
                return Content(op.ResultadoJson, "application/json");
        }

        if (!MercuryGo.Api.Utils.ValidadorEvidencia.EsEvidenciaValida(request.FotoUrl) ||
            !MercuryGo.Api.Utils.ValidadorEvidencia.EsEvidenciaValida(request.FirmaUrl))
        {
            return BadRequest(new { codigo = "evidencia_invalida", mensaje = "La evidencia de entrega (foto o firma) tiene un formato o URL no permitido." });
        }

        var pedido = await db.Pedidos.FirstOrDefaultAsync(p => p.Id == id && p.Activo, ct);
        if (pedido is null)
            return NotFound(new { codigo = "pedido_no_encontrado", mensaje = "El pedido no existe o fue cancelado." });

        var detalles = await db.DetallesPedido.Where(d => d.PedidoId == id).ToListAsync(ct);
        var mapaRechazos = (request.ItemsRechazados ?? []).ToDictionary(r => r.ProductoId);

        foreach (var d in detalles)
        {
            if (mapaRechazos.TryGetValue(d.ProductoId, out var rechazo))
            {
                var cantRechazada = Math.Min(d.Cantidad, Math.Max(0, rechazo.CantidadRechazada));
                d.CantidadRechazada = cantRechazada;
                d.CantidadEntregada = d.Cantidad - cantRechazada;
                d.MotivoRechazo = NormalizarTexto(rechazo.Motivo);

                // Devolver unidades lógicas y ajustar total por los productos rechazados en puerta
                if (cantRechazada > 0)
                {
                    d.Cantidad = d.CantidadEntregada.Value; // Ajuste del total de la orden
                }
            }
            else
            {
                d.CantidadEntregada = d.Cantidad;
                d.CantidadRechazada = 0;
            }
        }

        // Adjuntar prueba de entrega si se envió (CU-23)
        if (!string.IsNullOrWhiteSpace(request.FotoUrl)) pedido.EntregaFotoUrl = request.FotoUrl;
        if (!string.IsNullOrWhiteSpace(request.FirmaUrl)) pedido.EntregaFirmaUrl = request.FirmaUrl;

        var contexto = new MercuryGo.Api.Services.ContextoTransicion(
            UsuarioId: User.UsuarioId(),
            RolCodigo: User.RolCodigo() ?? string.Empty,
            Latitud: request.Latitud,
            Longitud: request.Longitud,
            EsSincronizacionOffline: request.EsOffline,
            OcurridoEn: request.OcurridoEn
        );

        var estadoAnterior = pedido.Estado;
        var resultado = await transicionService.TransicionarAsync(pedido, EstadoPedido.Entregado, contexto, ct);
        if (!resultado.Exito)
            return BadRequest(new { codigo = resultado.CodigoError, mensaje = resultado.MensajeError });

        await AvisarConflictosOfflineAsync(
            pedido, estadoAnterior, request.EsOffline, EstadoPedido.Entregado, ct);

        await db.SaveChangesAsync(ct);

        return Ok(new
        {
            mensaje = $"Entrega registrada con ajustes por ítems rechazados.",
            estado = pedido.Estado.ToString(),
            total_ajustado = detalles.Sum(d => d.Cantidad * d.PrecioUnitario)
        });
    }


    private static IContainer CeldaEncabezado(IContainer c) =>
        c.BorderBottom(1).BorderColor(Colors.Grey.Lighten1).PaddingVertical(5);

    private static IContainer CeldaCuerpo(IContainer c) =>
        c.BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten3).PaddingVertical(4);

    private static byte[] GenerarQrPedido(string numeroRemito)
    {
        using var generador = new QRCodeGenerator();
        using var datos = generador.CreateQrCode(numeroRemito, QRCodeGenerator.ECCLevel.M);
        return new PngByteQRCode(datos).GetGraphic(8);
    }

}