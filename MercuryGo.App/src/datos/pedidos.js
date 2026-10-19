const RETARDO_SIMULADO = 600;

const PEDIDOS = [
  { id: 'PED-0001', cliente: 'Almacén Don Tito', total: 52000, pago: 'Pendiente', estado: 'En preparación', color: 'warning' },
  { id: 'PED-0002', cliente: 'Autoservicio Central', total: 36000, pago: 'Pagado', estado: 'Listo', color: 'success' },
  { id: 'PED-0003', cliente: 'Minimercado Los Pinos', total: 6500, pago: 'Pagado', estado: 'Entregado', color: 'success' },
  { id: 'PED-0004', cliente: 'Despensa La Estrella', total: 9500, pago: 'Anulado', estado: 'Cancelado', color: 'danger' }
];

export function obtener_pedidos() {
  return new Promise(resolver => {
    setTimeout(() => resolver({ pedidos: PEDIDOS }), RETARDO_SIMULADO);
  });
}