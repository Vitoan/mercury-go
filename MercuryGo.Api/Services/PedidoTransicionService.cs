using Microsoft.EntityFrameworkCore;
using MercuryGo.Api.Auth;
using MercuryGo.Api.Data;
using MercuryGo.Api.Domain.Entities;

namespace MercuryGo.Api.Services;

public sealed class PedidoTransicionService(MercuryGoDbContext db) : IPedidoTransicionService
{
    // Regla de transición: (Origen, Destino) -> Roles autorizados
    private static readonly Dictionary<(EstadoPedido Origen, EstadoPedido Destino), string[]> ReglasTransicion = new()
    {
        // CU-19: Confirmar pedido (manual por el Admin)
        [(EstadoPedido.Pendiente, EstadoPedido.Confirmado)] = [RolCodigos.Admin],

        // Cancelación desde Pendiente
        [(EstadoPedido.Pendiente, EstadoPedido.Cancelado)] = [RolCodigos.Admin],

        // CU-11: Inicio de preparación / picking
        [(EstadoPedido.Confirmado, EstadoPedido.EnPreparacion)] = [RolCodigos.Operario, RolCodigos.Admin],
        [(EstadoPedido.Confirmado, EstadoPedido.Cancelado)] = [RolCodigos.Admin],

        // CU-11 + CU-20: Cierre de picking y despacho con chofer asignado
        [(EstadoPedido.EnPreparacion, EstadoPedido.Despachado)] = [RolCodigos.Operario, RolCodigos.Admin],
        [(EstadoPedido.EnPreparacion, EstadoPedido.Cancelado)] = [RolCodigos.Admin],

        // CU-12: En puerta
        [(EstadoPedido.Despachado, EstadoPedido.EnPuerta)] = [RolCodigos.Chofer, RolCodigos.Admin],
        [(EstadoPedido.Despachado, EstadoPedido.Entregado)] = [RolCodigos.Chofer, RolCodigos.Admin],
        [(EstadoPedido.Despachado, EstadoPedido.Cancelado)] = [RolCodigos.Admin], // Decisión 3: Admin puede cancelar en ruta

        // CU-13: Entrega final
        [(EstadoPedido.EnPuerta, EstadoPedido.Entregado)] = [RolCodigos.Chofer, RolCodigos.Admin],

        // CU-21: Entrega fallida (también directo desde Despachado: el chofer puede
        // no llegar a marcar "En Puerta" antes de fallar la entrega)
        [(EstadoPedido.EnPuerta, EstadoPedido.NoEntregado)] = [RolCodigos.Chofer, RolCodigos.Admin],
        [(EstadoPedido.Despachado, EstadoPedido.NoEntregado)] = [RolCodigos.Chofer, RolCodigos.Admin],
        [(EstadoPedido.EnPuerta, EstadoPedido.Cancelado)] = [RolCodigos.Admin],

        // CU-21: Reintento de entrega desde NoEntregado
        [(EstadoPedido.NoEntregado, EstadoPedido.Despachado)] = [RolCodigos.Admin],
        [(EstadoPedido.NoEntregado, EstadoPedido.Cancelado)] = [RolCodigos.Admin],

        // Decisión 10: Conflicto offline vs cancelación. Gana la acción del chofer
        [(EstadoPedido.Cancelado, EstadoPedido.Entregado)] = [RolCodigos.Chofer, RolCodigos.Admin]
    };

    public async Task<ResultadoTransicion> TransicionarAsync(
        Pedido pedido,
        EstadoPedido nuevoEstado,
        ContextoTransicion contexto,
        CancellationToken ct = default)
    {
        var estadoAnterior = pedido.Estado;

        // Idempotencia por estado: si ya se encuentra en el estado destino, responde éxito sin reejecutar
        if (estadoAnterior == nuevoEstado)
        {
            return ResultadoTransicion.Correcto(estadoAnterior, nuevoEstado);
        }

        // Manejo especial de la excepción offline (Decisión 10)
        if (estadoAnterior == EstadoPedido.Cancelado && nuevoEstado == EstadoPedido.Entregado)
        {
            if (!contexto.EsSincronizacionOffline && contexto.RolCodigo != RolCodigos.Admin)
            {
                return ResultadoTransicion.Fallo(
                    "pedido_cancelado",
                    "El pedido fue cancelado y no puede ser marcado como entregado en operaciones en línea."
                );
            }
        }

        // Evaluar validez de la transición según el mapa de reglas
        var clave = (estadoAnterior, nuevoEstado);
        if (!ReglasTransicion.TryGetValue(clave, out var rolesPermitidos))
        {
            return ResultadoTransicion.Fallo(
                "transicion_invalida",
                $"No es posible cambiar el estado de '{estadoAnterior}' a '{nuevoEstado}'."
            );
        }

        // Validar autorización de rol
        if (!rolesPermitidos.Contains(contexto.RolCodigo))
        {
            return ResultadoTransicion.Fallo(
                "rol_no_autorizado",
                $"El rol '{contexto.RolCodigo}' no tiene autorización para realizar la transición '{estadoAnterior}' → '{nuevoEstado}'."
            );
        }

        // Aplicar la transición
        pedido.Estado = nuevoEstado;
        pedido.ActualizadoEn = DateTime.UtcNow;

        var marcaTiempo = contexto.OcurridoEn ?? DateTime.UtcNow;

        // Efectos colaterales por estado destino
        switch (nuevoEstado)
        {
            case EstadoPedido.Confirmado:
                pedido.ConfirmadoPorId = contexto.UsuarioId;
                pedido.ConfirmadoEn = marcaTiempo;
                break;

            case EstadoPedido.EnPuerta:
                pedido.FechaEnPuerta = marcaTiempo;
                if (contexto.Latitud.HasValue && contexto.Longitud.HasValue)
                {
                    pedido.LatitudEntrega = contexto.Latitud.Value;
                    pedido.LongitudEntrega = contexto.Longitud.Value;
                }
                break;

            case EstadoPedido.Entregado:
                pedido.EntregadoEn = marcaTiempo;
                if (contexto.Latitud.HasValue && contexto.Longitud.HasValue)
                {
                    pedido.LatitudEntrega = contexto.Latitud.Value;
                    pedido.LongitudEntrega = contexto.Longitud.Value;
                }

                if (estadoAnterior == EstadoPedido.Cancelado)
                {
                    var detallesEntregados = await db.DetallesPedido
                        .Include(d => d.Producto)
                        .Where(d => d.PedidoId == pedido.Id)
                        .ToListAsync(ct);

                    foreach (var det in detallesEntregados)
                    {
                        if (det.Producto != null)
                        {
                            det.Producto.StockActual -= det.Cantidad;
                            det.Producto.ActualizadoEn = marcaTiempo;
                        }
                    }

                    var aviso = "[Aviso Sincronización Offline: Entrega registrada por chofer en destino]";
                    pedido.Observaciones = string.IsNullOrWhiteSpace(pedido.Observaciones)
                        ? aviso
                        : $"{pedido.Observaciones} · {aviso}";
                }
                break;

            case EstadoPedido.Cancelado:
                // Si el pedido se cancela, se restituye el stock reservado de cada producto
                if (estadoAnterior != EstadoPedido.Cancelado)
                {
                    var detalles = await db.DetallesPedido
                        .Include(d => d.Producto)
                        .Where(d => d.PedidoId == pedido.Id)
                        .ToListAsync(ct);

                    foreach (var det in detalles)
                    {
                        if (det.Producto != null)
                        {
                            det.Producto.StockActual += det.Cantidad;
                            if (det.Producto.StockActual > 0)
                            {
                                det.Producto.Disponible = true;
                            }
                            det.Producto.ActualizadoEn = marcaTiempo;
                        }
                    }
                }
                break;
        }

        // Crear notificaciones in-app y push para el cliente y los roles operativos involucrados
        await GenerarNotificacionClienteAsync(pedido, nuevoEstado, ct);
        await GenerarNotificacionesOperativasAsync(pedido, nuevoEstado, contexto, ct);

        return ResultadoTransicion.Correcto(estadoAnterior, nuevoEstado);
    }

    private async Task GenerarNotificacionesOperativasAsync(
        Pedido pedido,
        EstadoPedido nuevoEstado,
        ContextoTransicion contexto,
        CancellationToken ct)
    {
        var choferId = pedido.ChoferId ?? (contexto.RolCodigo == RolCodigos.Chofer ? contexto.UsuarioId : (long?)null);

        switch (nuevoEstado)
        {
            case EstadoPedido.Confirmado:
                await NotificarRolInternoAsync(
                    RolCodigos.Admin,
                    $"📋 Pedido confirmado ({pedido.Numero})",
                    $"La orden {pedido.Numero} fue confirmada y enviada a preparación de depósito.",
                    "PEDIDO",
                    pedido.Id,
                    ct);
                await NotificarRolInternoAsync(
                    RolCodigos.Operario,
                    $"📦 Pedido listo para picking ({pedido.Numero})",
                    $"La orden {pedido.Numero} fue confirmada por administración y está lista para armar en depósito.",
                    "PEDIDO",
                    pedido.Id,
                    ct);
                break;

            case EstadoPedido.EnPreparacion:
                await NotificarRolInternoAsync(
                    RolCodigos.Admin,
                    $"📦 Pedido en preparación ({pedido.Numero})",
                    $"La orden {pedido.Numero} se encuentra en preparación en el depósito.",
                    "PEDIDO",
                    pedido.Id,
                    ct);
                await NotificarRolInternoAsync(
                    RolCodigos.Operario,
                    $"📦 Preparación iniciada ({pedido.Numero})",
                    $"La orden {pedido.Numero} pasó al estado En Depósito.",
                    "PEDIDO",
                    pedido.Id,
                    ct);
                break;

            case EstadoPedido.Despachado:
                await NotificarRolInternoAsync(
                    RolCodigos.Admin,
                    $"🚚 Pedido despachado ({pedido.Numero})",
                    $"La orden {pedido.Numero} salió del depósito hacia reparto.",
                    "PEDIDO",
                    pedido.Id,
                    ct);
                await NotificarRolInternoAsync(
                    RolCodigos.Operario,
                    $"🚚 Orden despachada ({pedido.Numero})",
                    $"La orden {pedido.Numero} fue despachada hacia la ruta de entrega.",
                    "PEDIDO",
                    pedido.Id,
                    ct);
                if (choferId.HasValue)
                {
                    await NotificarUsuarioInternoAsync(
                        choferId.Value,
                        $"🛣️ Orden activa en tu ruta ({pedido.Numero})",
                        $"La orden {pedido.Numero} fue despachada y está lista para entrega en tu hoja de ruta.",
                        "PEDIDO",
                        pedido.Id,
                        ct);
                }
                break;

            case EstadoPedido.EnPuerta:
                await NotificarRolInternoAsync(
                    RolCodigos.Admin,
                    $"📍 Chofer en puerta ({pedido.Numero})",
                    $"El repartidor arribó al domicilio del cliente para la orden {pedido.Numero}.",
                    "GPS",
                    pedido.Id,
                    ct);
                if (choferId.HasValue)
                {
                    await NotificarUsuarioInternoAsync(
                        choferId.Value,
                        $"📍 Arribo en puerta registrado ({pedido.Numero})",
                        $"Se registró tu ubicación en puerta para la orden {pedido.Numero}.",
                        "GPS",
                        pedido.Id,
                        ct);
                }
                break;

            case EstadoPedido.Entregado:
                await NotificarRolInternoAsync(
                    RolCodigos.Admin,
                    $"✅ Pedido entregado ({pedido.Numero})",
                    $"El chofer completó la entrega en destino de la orden {pedido.Numero}.",
                    "PEDIDO",
                    pedido.Id,
                    ct);
                if (choferId.HasValue)
                {
                    await NotificarUsuarioInternoAsync(
                        choferId.Value,
                        $"✅ Entrega completada ({pedido.Numero})",
                        $"Completaste satisfactoriamente la entrega de la orden {pedido.Numero}.",
                        "PEDIDO",
                        pedido.Id,
                        ct);
                }
                break;

            case EstadoPedido.NoEntregado:
                await NotificarRolInternoAsync(
                    RolCodigos.Admin,
                    $"⚠️ Entrega fallida en calle ({pedido.Numero})",
                    $"El repartidor reportó que no se pudo concretar la entrega de la orden {pedido.Numero}.",
                    "PEDIDO",
                    pedido.Id,
                    ct);
                if (choferId.HasValue)
                {
                    await NotificarUsuarioInternoAsync(
                        choferId.Value,
                        $"⚠️ Intento fallido registrado ({pedido.Numero})",
                        $"Quedó asentado el intento de entrega no concretado de la orden {pedido.Numero}.",
                        "PEDIDO",
                        pedido.Id,
                        ct);
                }
                break;

            case EstadoPedido.Cancelado:
                await NotificarRolInternoAsync(
                    RolCodigos.Admin,
                    $"❌ Pedido cancelado ({pedido.Numero})",
                    $"La orden {pedido.Numero} fue cancelada y su stock restituido.",
                    "PEDIDO",
                    pedido.Id,
                    ct);
                await NotificarRolInternoAsync(
                    RolCodigos.Operario,
                    $"❌ Orden cancelada ({pedido.Numero})",
                    $"La orden {pedido.Numero} fue cancelada.",
                    "PEDIDO",
                    pedido.Id,
                    ct);
                if (choferId.HasValue)
                {
                    await NotificarUsuarioInternoAsync(
                        choferId.Value,
                        $"❌ Orden cancelada ({pedido.Numero})",
                        $"La orden {pedido.Numero} asignada a tu ruta fue cancelada.",
                        "PEDIDO",
                        pedido.Id,
                        ct);
                }
                break;
        }
    }

    private async Task NotificarRolInternoAsync(
        string rolCodigo,
        string titulo,
        string mensaje,
        string tipo,
        long pedidoId,
        CancellationToken ct)
    {
        var roles = await db.Roles.AsNoTracking()
            .Where(r => r.Codigo == rolCodigo)
            .Select(r => r.Id)
            .ToListAsync(ct);

        if (roles.Count == 0)
            return;

        var usuariosIds = await db.Usuarios.AsNoTracking()
            .Where(u => u.Activo && u.RolId.HasValue && roles.Contains(u.RolId.Value))
            .Select(u => u.Id)
            .ToListAsync(ct);

        var ahora = DateTime.UtcNow;
        foreach (var uid in usuariosIds)
        {
            db.Notificaciones.Add(new Notificacion
            {
                UsuarioId = uid,
                Titulo = titulo,
                Mensaje = mensaje,
                Tipo = tipo,
                ReferenciaId = pedidoId,
                CreadoEn = ahora
            });
        }
    }

    private async Task NotificarUsuarioInternoAsync(
        long usuarioId,
        string titulo,
        string mensaje,
        string tipo,
        long pedidoId,
        CancellationToken ct)
    {
        var existe = await db.Usuarios.AsNoTracking()
            .AnyAsync(u => u.Id == usuarioId && u.Activo, ct);
        if (!existe)
            return;

        db.Notificaciones.Add(new Notificacion
        {
            UsuarioId = usuarioId,
            Titulo = titulo,
            Mensaje = mensaje,
            Tipo = tipo,
            ReferenciaId = pedidoId,
            CreadoEn = DateTime.UtcNow
        });
    }

    private async Task GenerarNotificacionClienteAsync(Pedido pedido, EstadoPedido nuevoEstado, CancellationToken ct)
    {
        var usuarioCliente = await db.ResolverUsuarioDeClienteAsync(pedido.ClienteId, ct);
        if (usuarioCliente is null)
            return;

        string titulo;
        string mensaje;
        string tipo = "PEDIDO";

        switch (nuevoEstado)
        {
            case EstadoPedido.Confirmado:
                titulo = $"📋 Pedido confirmado ({pedido.Numero})";
                mensaje = $"Tu orden {pedido.Numero} ha sido confirmada por el depósito y entrará en preparación.";
                break;

            case EstadoPedido.EnPreparacion:
                titulo = $"📦 Pedido en preparación ({pedido.Numero})";
                mensaje = $"El equipo de logística se encuentra realizando el picking de los productos de tu orden {pedido.Numero}.";
                break;

            case EstadoPedido.Despachado:
                titulo = $"🚚 Pedido despachado ({pedido.Numero})";
                mensaje = $"Tu pedido {pedido.Numero} ha salido del depósito rumbo a tu domicilio.";
                break;

            case EstadoPedido.EnPuerta:
                titulo = $"📍 ¡Repartidor en puerta! ({pedido.Numero})";
                mensaje = $"El chofer ha llegado al domicilio de entrega para tu orden {pedido.Numero}.";
                tipo = "GPS";
                break;

            case EstadoPedido.Entregado:
                titulo = $"✅ Pedido entregado ({pedido.Numero})";
                mensaje = $"La orden {pedido.Numero} fue completada y entregada satisfactoriamente.";
                break;

            case EstadoPedido.NoEntregado:
                titulo = $"⚠️ Entrega no concretada ({pedido.Numero})";
                mensaje = $"No pudimos completar la entrega de la orden {pedido.Numero}. Nos contactaremos a la brevedad.";
                break;

            case EstadoPedido.Cancelado:
                titulo = $"❌ Pedido cancelado ({pedido.Numero})";
                mensaje = $"La orden {pedido.Numero} ha sido cancelada en el sistema.";
                break;

            default:
                titulo = $"Actualización de Pedido ({pedido.Numero})";
                mensaje = $"El pedido {pedido.Numero} pasó al estado {nuevoEstado}.";
                break;
        }

        db.Notificaciones.Add(new Notificacion
        {
            UsuarioId = usuarioCliente.Id,
            Titulo = titulo,
            Mensaje = mensaje,
            Tipo = tipo,
            ReferenciaId = pedido.Id,
            CreadoEn = DateTime.UtcNow
        });
    }
}
