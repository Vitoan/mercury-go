import { createRouter, createWebHistory } from '@ionic/vue-router';
import main_layout from '../layouts/main_layout.vue';
import { navegacion } from '../config/navegacion';
import { sesion_store } from '@/stores/sesion_store';

const rutas_app = navegacion.map(item => ({
  path: item.ruta.replace('/app/', ''),
  name: item.id,
  component: item.componente,
  meta: {
    roles: item.roles
  }
}));

const routes = [
  {
    path: '/',
    redirect: '/app/inicio'
  },
  {
    path: '/login',
    name: 'login',
    component: () => import('@/views/login_page.vue'),
    meta: {
      publica: true
    }
  },
  {
    path: '/app',
    component: main_layout,
    children: [
      { path: '', redirect: '/app/inicio' },
      ...rutas_app
    ]
  },
  {
    path: '/:pathMatch(.*)*',
    redirect: '/app/inicio'
  }
];

const router = createRouter({
  history: createWebHistory(import.meta.env.BASE_URL),
  routes
});

// Guard de navegación por rol.
// Bloquea rutas no públicas si no hay autenticación,
// y si el rol del usuario no tiene acceso a la pantalla, redirige a Inicio.
router.beforeEach((to) => {
  // Mientras se restaura la sesión al arrancar no bloqueamos prematuramente
  if (sesion_store.restaurando) return true;

  if (to.meta.publica) {
    return sesion_store.autenticado.value ? { path: '/app/inicio', replace: true } : true;
  }

  if (!sesion_store.autenticado.value) {
    return { path: '/login', replace: true };
  }

  const rolesPermitidos = to.meta.roles;
  if (!rolesPermitidos || rolesPermitidos.length === 0) return true;

  const rolActivo = sesion_store.rol_activo.value;

  // Si el usuario no tiene rol asignado (pendiente de habilitación), solo accede a inicio y cuenta
  if (!rolActivo) {
    if (to.name === 'inicio' || to.name === 'cuenta') return true;
    return { path: '/app/cuenta', replace: true };
  }

  if (rolesPermitidos.includes(rolActivo)) return true;

  // Si su rol no alcanza para esta vista, redirigir a inicio
  return { path: '/app/inicio', replace: true };
});

export default router;