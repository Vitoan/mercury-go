import {
  homeOutline,
  cubeOutline,
  businessOutline,
  receiptOutline,
  peopleOutline,
  personCircleOutline
} from 'ionicons/icons';

// Matriz de navegación por roles logísticos para MercuryGO.
// Roles: ADMIN, OPERARIO, CHOFER, CLIENTE.
// Un usuario sin rol solo accede a 'inicio' y 'cuenta'.
export const navegacion = [
  {
    id: 'inicio',
    grupo_menu: 'principal',
    titulo: 'Inicio',
    orden: 10,
    ruta: '/app/inicio',
    icono: homeOutline,
    componente: () => import('@/views/inicio_page.vue'),
    en_tabs: true,
    roles: ['ADMIN', 'OPERARIO', 'CHOFER', 'CLIENTE']
  },
  {
    id: 'productos',
    grupo_menu: 'principal',
    titulo: 'Catálogo',
    orden: 20,
    ruta: '/app/productos',
    icono: cubeOutline,
    componente: () => import('@/views/productos_page.vue'),
    en_tabs: true,
    roles: ['ADMIN', 'OPERARIO', 'CLIENTE']
  },
  {
    id: 'clientes',
    grupo_menu: 'principal',
    titulo: 'Clientes',
    orden: 30,
    ruta: '/app/clientes',
    icono: businessOutline,
    componente: () => import('@/views/clientes_page.vue'),
    en_tabs: true,
    roles: ['ADMIN', 'OPERARIO']
  },
  {
    id: 'pedidos',
    grupo_menu: 'principal',
    titulo: 'Pedidos',
    orden: 40,
    ruta: '/app/pedidos',
    icono: receiptOutline,
    componente: () => import('@/views/pedidos_page.vue'),
    en_tabs: true,
    roles: ['ADMIN', 'OPERARIO', 'CHOFER', 'CLIENTE']
  },
  {
    id: 'usuarios',
    grupo_menu: 'administracion',
    titulo: 'Usuarios',
    orden: 45,
    ruta: '/app/usuarios',
    icono: peopleOutline,
    componente: () => import('@/views/usuarios_page.vue'),
    en_tabs: false, // Accesible desde menú lateral para ADMIN
    roles: ['ADMIN']
  },
  {
    id: 'cuenta',
    grupo_menu: 'configuracion',
    titulo: 'Mi cuenta',
    orden: 50,
    ruta: '/app/cuenta',
    icono: personCircleOutline,
    componente: () => import('@/views/cuenta_page.vue'),
    en_tabs: true,
    roles: ['ADMIN', 'OPERARIO', 'CHOFER', 'CLIENTE']
  }
];