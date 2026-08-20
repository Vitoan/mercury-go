import { homeOutline, pricetagsOutline, peopleOutline, receiptOutline, personCircleOutline } from 'ionicons/icons';

export const navegacion = [
  {
    id: 'inicio',
    grupo_menu: 'principal',
    titulo: 'Inicio',
    orden: 10,
    ruta: '/app/inicio',
    icono: homeOutline,
    componente: () => import('@/views/inicio_page.vue'),
    en_tabs: true
  },
  {
    id: 'productos',
    grupo_menu: 'principal',
    titulo: 'Productos',
    orden: 20,
    ruta: '/app/productos',
    icono: pricetagsOutline,
    componente: () => import('@/views/productos_page.vue'),
    en_tabs: true
  },
  {
    id: 'clientes',
    grupo_menu: 'principal',
    titulo: 'Clientes',
    orden: 30,
    ruta: '/app/clientes',
    icono: peopleOutline,
    componente: () => import('@/views/clientes_page.vue'),
    en_tabs: true
  },
  {
    id: 'pedidos',
    grupo_menu: 'principal',
    titulo: 'Pedidos',
    orden: 40,
    ruta: '/app/pedidos',
    icono: receiptOutline,
    componente: () => import('@/views/pedidos_page.vue'),
    en_tabs: true
  },
  {
    id: 'cuenta',
    grupo_menu: 'configuracion',
    titulo: 'Mi cuenta',
    orden: 50,
    ruta: '/app/cuenta',
    icono: personCircleOutline,
    componente: () => import('@/views/cuenta_page.vue'),
    en_tabs: true
  }
];