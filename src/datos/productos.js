const RETARDO_SIMULADO = 600;

const PRODUCTOS = [
  { id: 1, nombre: 'Aceite de Girasol 1.5L', detalle: 'Caja x 12 unidades', precio: 2400, stock: true },
  { id: 2, nombre: 'Harina 000 1kg', detalle: 'Pack x 10 unidades', precio: 950, stock: true },
  { id: 3, nombre: 'Arroz Largo Fino 1kg', detalle: 'Bolsa x 10 unidades', precio: 1800, stock: false },
  { id: 4, nombre: 'Fideos Guiseros 500g', detalle: 'Caja x 20 paquetes', precio: 1100, stock: true },
  { id: 5, nombre: 'Azúcar Común 1kg', detalle: 'Fardo x 10 unidades', precio: 1250, stock: true },
  { id: 6, nombre: 'Yerba Mate 1kg', detalle: 'Pack x 6 unidades', precio: 3400, stock: true }
];

export function obtener_productos() {
  return new Promise(resolver => {
    setTimeout(() => resolver({ productos: PRODUCTOS }), RETARDO_SIMULADO);
  });
}