import { ajax_service } from './ajax_service';

export const pedidos_service = {
  obtener_resumen() {
    return ajax_service.get('api/pedidos/resumen');
  },

  obtener_por_id(id) {
    return ajax_service.get(`api/pedidos/${id}`);
  }
};
