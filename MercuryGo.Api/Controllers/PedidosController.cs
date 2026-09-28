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

namespace MercuryGo.Api.Controllers;

[ApiController]
[Authorize(Roles = $"{RolCodigos.Admin},{RolCodigos.Operario},{RolCodigos.Chofer},{RolCodigos.Cliente}")]
[Route("api/pedidos")]
public sealed class PedidosController(MercuryGoDbContext db, IWebHostEnvironment env) : ControllerBase
{
    // Diccionario de la máquina de estados de un pedido logístico.
    // La validación vive estrictamente en el servidor.
    private static readonly Dictionary<EstadoPedido, EstadoPedido[]> TransicionesValidas = new()
    {
        [EstadoPedido.Pendiente] = [EstadoPedido.Confirmado, EstadoPedido.Cancelado],
        [EstadoPedido.Confirmado] = [EstadoPedido.EnPreparacion, EstadoPedido.Cancelado],
        [EstadoPedido.EnPreparacion] = [EstadoPedido.Despachado, EstadoPedido.Cancelado],
        [EstadoPedido.Despachado] = [EstadoPedido.Entregado, EstadoPedido.Cancelado],
        [EstadoPedido.Entregado] = [], // Estado final: no admite cambios posteriores
        [EstadoPedido.Cancelado] = []  // Estado final
    };

    // Listado paginado con filtros de texto, estados múltiples ("Pendiente,EnPreparacion") y cliente.
    [HttpGet]
    public async Task<IActionResult> Listar(
        [FromQuery] string? busqueda,
        [FromQuery] string? estados,
        [FromQuery] long? cliente_id,
        [FromQuery] int? pagina,
        [FromQuery] int? tamano,
        CancellationToken ct)
    {
        var consulta = db.Pedidos.AsNoTracking().Where(p => p.Activo);

        var texto = (busqueda ?? string.Empty).Trim();
        if (texto.Length > 0)
        {
            consulta = consulta.Where(p => p.Numero.Contains(texto) || p.Cliente!.RazonSocial.Contains(texto));
        }

        if (cliente_id.HasValue && cliente_id.Value > 0)
        {
            consulta = consulta.Where(p => p.ClienteId == cliente_id.Value);
        }

        var listaEstados = FiltroEnum.Parsear<EstadoPedido>(estados);
        if (listaEstados.Count > 0)
        {
            consulta = consulta.Where(p => listaEstados.Contains(p.Estado));
        }

        var (items, paginaResponse) = await (
            from pedido in consulta
            join cliente in db.Clientes.AsNoTracking() on pedido.ClienteId equals cliente.Id
            orderby pedido.FechaPedido descending
            select new PedidoListaResponse(
                pedido.Id,
                pedido.Numero,
                pedido.Estado.ToString(),
                pedido.FechaPedido,
                cliente.Id,
                cliente.RazonSocial,
                Math.Round(
                    db.DetallesPedido
                        .Where(d => d.PedidoId == pedido.Id)
                        .Sum(d => (decimal?)d.Cantidad * d.PrecioUnitario) ?? 0m,
                    2),
                pedido.Observaciones
            )
        ).PaginarAsync(PaginaConsulta.Desde(pagina, tamano), ct);

        return Ok(new { pedidos = items, pagina = paginaResponse });
    }

    // Resumen general de conteos por estado (para métricas y badges).
    [HttpGet("resumen")]
    public async Task<IActionResult> Resumen(CancellationToken ct)
    {
        var conteosPorEstado = await db.Pedidos
            .AsNoTracking()
            .Where(p => p.Activo)
            .GroupBy(p => p.Estado)
            .Select(g => new { estado = g.Key.ToString(), cantidad = g.Count() })
            .ToListAsync(ct);

        var total = await db.Pedidos.CountAsync(p => p.Activo, ct);

        return Ok(new { conteos_por_estado = conteosPorEstado, total });
    }

    [HttpGet("{id:long}")]
    public async Task<IActionResult> Detalle(long id, CancellationToken ct)
    {
        var pedido = await (
            from p in db.Pedidos.AsNoTracking()
            join c in db.Clientes.AsNoTracking() on p.ClienteId equals c.Id
            where p.Id == id && p.Activo
            select new { p, c }
        ).FirstOrDefaultAsync(ct);

        if (pedido is null)
            return NotFound(new { codigo = "pedido_no_encontrado", mensaje = "El remito/orden de carga no existe o fue cancelado." });

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

        var resultado = new PedidoDetalleResponse(
            pedido.p.Id,
            pedido.p.Numero,
            pedido.p.Estado.ToString(),
            pedido.p.FechaPedido,
            pedido.p.Observaciones,
            new ClientePedidoResponse(pedido.c.Id, pedido.c.RazonSocial, pedido.c.Cuit),
            detalles,
            total
        );

        return Ok(new { pedido = resultado });
    }

    // Creación de orden de carga: LOS PRECIOS NO VIAJAN DESDE EL TELÉFONO.
    // El servidor lee los precios vigentes de la base de datos para preservar la integridad contable.
    [HttpPost]
    public async Task<IActionResult> Crear([FromBody] CrearPedidoRequest request, CancellationToken ct)
    {
        if (request.ClienteId <= 0)
            return BadRequest(new { codigo = "cliente_requerido", mensaje = "Debe especificar un cliente comercial válido." });

        var clienteExiste = await db.Clientes.AnyAsync(c => c.Id == request.ClienteId && c.Activo, ct);
        if (!clienteExiste)
            return BadRequest(new { codigo = "cliente_no_encontrado", mensaje = "El cliente seleccionado no existe o está inactivo." });

        if (request.Items is null || request.Items.Count == 0)
            return BadRequest(new { codigo = "items_requeridos", mensaje = "La orden de carga debe contener al menos un producto." });

        var productosIds = request.Items.Select(i => i.ProductoId).Distinct().ToList();
        var productosDb = await db.Productos
            .Where(p => productosIds.Contains(p.Id) && p.Activo)
            .ToDictionaryAsync(p => p.Id, ct);

        foreach (var item in request.Items)
        {
            if (!productosDb.TryGetValue(item.ProductoId, out var prod))
                return BadRequest(new { codigo = "producto_invalido", mensaje = $"El producto con ID {item.ProductoId} no existe o está inactivo." });

            if (item.Cantidad <= 0)
                return BadRequest(new { codigo = "cantidad_invalida", mensaje = $"La cantidad para '{prod.Nombre}' debe ser mayor a cero." });
        }

        // Generar número de remito correlativo
        var totalPedidos = await db.Pedidos.CountAsync(ct);
        var numeroRemito = $"PED-{(totalPedidos + 1):D6}";

        var nuevoPedido = new Pedido
        {
            ClienteId = request.ClienteId,
            Numero = numeroRemito,
            Estado = EstadoPedido.Pendiente,
            FechaPedido = DateTime.UtcNow,
            Observaciones = NormalizarTexto(request.Observaciones),
            Activo = true,
            CreadoEn = DateTime.UtcNow
        };

        db.Pedidos.Add(nuevoPedido);
        await db.SaveChangesAsync(ct);

        foreach (var item in request.Items)
        {
            var prod = productosDb[item.ProductoId];
            db.DetallesPedido.Add(new DetallePedido
            {
                PedidoId = nuevoPedido.Id,
                ProductoId = prod.Id,
                Cantidad = item.Cantidad,
                PrecioUnitario = prod.Precio, // Precio histórico congelado al crear
                CreadoEn = DateTime.UtcNow
            });
        }

        await db.SaveChangesAsync(ct);

        return Ok(new { pedido_id = nuevoPedido.Id, numero = nuevoPedido.Numero });
    }

    // Transición de estado evaluada con la máquina de estados del servidor.
    [HttpPut("{id:long}/estado")]
    public async Task<IActionResult> CambiarEstado(long id, [FromBody] CambiarEstadoRequest request, CancellationToken ct)
    {
        if (!Enum.TryParse<EstadoPedido>(request.Estado, true, out var nuevoEstado))
            return BadRequest(new { codigo = "estado_invalido", mensaje = $"El estado '{request.Estado}' no es un estado logístico reconocido." });

        var pedido = await db.Pedidos.FirstOrDefaultAsync(p => p.Id == id && p.Activo, ct);
        if (pedido is null)
            return NotFound(new { codigo = "pedido_no_encontrado", mensaje = "El pedido no existe o fue dado de baja." });

        if (pedido.Estado == nuevoEstado)
            return Ok(new { estado = pedido.Estado.ToString() });

        if (!TransicionesValidas.TryGetValue(pedido.Estado, out var destinosPermitidos) || !destinosPermitidos.Contains(nuevoEstado))
        {
            return BadRequest(new
            {
                codigo = "transicion_invalida",
                mensaje = $"No es posible cambiar el estado de '{pedido.Estado}' a '{nuevoEstado}'. Transición rechazada por el servidor."
            });
        }

        pedido.Estado = nuevoEstado;
        pedido.ActualizadoEn = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);

        return Ok(new { estado = pedido.Estado.ToString() });
    }

    // Cancelación lógica de una orden de carga
    [HttpDelete("{id:long}")]
    public async Task<IActionResult> Cancelar(long id, CancellationToken ct)
    {
        var pedido = await db.Pedidos.FirstOrDefaultAsync(p => p.Id == id && p.Activo, ct);
        if (pedido is null)
            return NotFound(new { codigo = "pedido_no_encontrado", mensaje = "El pedido no existe o ya fue cancelado." });

        if (pedido.Estado == EstadoPedido.Entregado)
            return BadRequest(new { codigo = "pedido_ya_entregado", mensaje = "Una orden entregada no puede cancelarse." });

        pedido.Estado = EstadoPedido.Cancelado;
        pedido.ActualizadoEn = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);

        return Ok(new { cancelado = true });
    }

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

        if (pedido.Estado == EstadoPedido.Entregado || pedido.Estado == EstadoPedido.Cancelado)
            return BadRequest(new { codigo = "estado_invalido", mensaje = "No se puede realizar picking sobre un pedido finalizado o cancelado." });

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
                var enFalta = string.Equals(pick.EstadoItem, "EnFalta", StringComparison.OrdinalIgnoreCase) || pick.CantidadPreparada < item.d.Cantidad;
                if (enFalta)
                {
                    esCompleto = false;
                    faltantes.Add($"{item.ProductoNombre} (prep. {pick.CantidadPreparada}/{item.d.Cantidad})");
                }
            }
            else
            {
                esCompleto = false;
                faltantes.Add($"{item.ProductoNombre} (no verificado)");
            }
        }

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
        await db.SaveChangesAsync(ct);

        return Ok(new
        {
            completo = esCompleto,
            estado = pedido.Estado.ToString(),
            faltantes,
            observaciones = pedido.Observaciones,
            mensaje = esCompleto
                ? "Picking completado exitosamente. Todo listo para despacho."
                : "Picking registrado con faltantes para revisión del Administrador."
        });
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

        var webRoot = env.WebRootPath ?? Path.Combine(env.ContentRootPath, "wwwroot");
        var carpetaComprobantes = Path.Combine(webRoot, "uploads", "comprobantes");
        Directory.CreateDirectory(carpetaComprobantes);

        var nombreArchivo = $"{Guid.NewGuid():N}{extension}";
        var rutaFisica = Path.Combine(carpetaComprobantes, nombreArchivo);
        await using var stream = System.IO.File.Create(rutaFisica);
        await archivo.CopyToAsync(stream, ct);

        // Guardamos la referencia en observaciones si no hay columna o para registro histórico
        var etiquetaComprobante = $"[Comprobante de pago: /uploads/comprobantes/{nombreArchivo}]";
        if (string.IsNullOrWhiteSpace(pedido.Observaciones))
            pedido.Observaciones = etiquetaComprobante;
        else if (!pedido.Observaciones.Contains("/uploads/comprobantes/"))
            pedido.Observaciones = $"{pedido.Observaciones} · {etiquetaComprobante}";

        pedido.ActualizadoEn = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);

        return Ok(new
        {
            comprobante_url = $"/uploads/comprobantes/{nombreArchivo}",
            mensaje = "Comprobante de pago adjuntado exitosamente a la orden."
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

    // Código QR en PNG con QRCoder conteniendo el número de remito (PED-00000X)
    [HttpGet("{id:long}/qr")]
    public async Task<IActionResult> Qr(long id, CancellationToken ct)
    {
        var pedido = await db.Pedidos.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id && p.Activo, ct);
        if (pedido is null)
            return NotFound(new { codigo = "pedido_no_encontrado", mensaje = "El pedido no existe." });

        var qrBytes = GenerarQrPedido(pedido.Numero);
        return File(qrBytes, "image/png", $"{pedido.Numero}-qr.png");
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

    private static string? NormalizarTexto(string? texto)
    {
        var limpio = (texto ?? string.Empty).Trim();
        return limpio.Length > 0 ? limpio : null;
    }
}

public sealed record CrearPedidoItemRequest(long ProductoId, int Cantidad);
public sealed record CrearPedidoRequest(long ClienteId, string? Observaciones, List<CrearPedidoItemRequest> Items);
public sealed record CambiarEstadoRequest(string Estado);
public sealed record ItemPickingRequest(long ProductoId, string EstadoItem, int CantidadPreparada);
public sealed record ActualizarPickingRequest(List<ItemPickingRequest> Items);

public sealed record PedidoListaResponse(
    long Id,
    string Numero,
    string Estado,
    DateTime FechaPedido,
    long ClienteId,
    string ClienteRazonSocial,
    decimal Total,
    string? Observaciones
);

public sealed record PedidoDetalleResponse(
    long Id,
    string Numero,
    string Estado,
    DateTime FechaPedido,
    string? Observaciones,
    ClientePedidoResponse Cliente,
    IEnumerable<DetallePedidoResponse> Detalles,
    decimal Total
);

public sealed record ClientePedidoResponse(long Id, string RazonSocial, string Cuit);

public sealed record DetallePedidoResponse(
    long Id,
    long ProductoId,
    string ProductoNombre,
    int Cantidad,
    decimal PrecioUnitario,
    decimal Subtotal
);
