import { createRouter, createWebHistory } from '@ionic/vue-router';
import main_layout from '../layouts/main_layout.vue';
import { navegacion } from '../config/navegacion';

const rutas_app = navegacion.map(item => ({
  path: item.ruta.replace('/app/', ''),
  name: item.id,
  component: item.componente
}));

const routes = [
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

export default router;