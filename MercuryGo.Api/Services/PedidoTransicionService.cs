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

        // CU-21: Entrega fallida
        [(EstadoPedido.EnPuerta, EstadoPedido.NoEntregado)] = [RolCodigos.Chofer, RolCodigos.Admin],
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
                break;

            case EstadoPedido.Cancelado:
                // Si el pedido se cancela, se marca la devolución lógica de la orden
                break;
        }

        // Crear notificación in-app para el cliente
        await GenerarNotificacionClienteAsync(pedido, nuevoEstado, ct);

        return ResultadoTransicion.Correcto(estadoAnterior, nuevoEstado);
    }

    private async Task GenerarNotificacionClienteAsync(Pedido pedido, EstadoPedido nuevoEstado, CancellationToken ct)
    {
        var cliente = await db.Clientes.AsNoTracking().FirstOrDefaultAsync(c => c.Id == pedido.ClienteId, ct);
        if (cliente is null || string.IsNullOrWhiteSpace(cliente.Email))
            return;

        var usuarioCliente = await db.Usuarios.AsNoTracking().FirstOrDefaultAsync(u => u.Email == cliente.Email, ct);
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
