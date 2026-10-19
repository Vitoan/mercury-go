const RETARDO_SIMULADO = 600;

const CLIENTES = [
  { id: 101, nombre: 'Almacén Don Tito', telefono: '266-455-1001', direccion: 'Av. Illia 340, San Luis' },
  { id: 102, nombre: 'Autoservicio Central', telefono: '266-455-1002', direccion: 'Colón 545, San Luis' },
  { id: 103, nombre: 'Minimercado Los Pinos', telefono: '266-455-1003', direccion: 'Junín 1120, San Luis' },
  { id: 104, nombre: 'Despensa La Estrella', telefono: '266-455-1004', direccion: 'Sin dirección' }
];

export function obtener_clientes() {
  return new Promise(resolver => {
    setTimeout(() => resolver({ clientes: CLIENTES }), RETARDO_SIMULADO);
  });
}