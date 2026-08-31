import { ajax_service } from './ajax_service';

export const clientes_service = {
  obtener_clientes() {
    return ajax_service.get('api/clientes');
  },

  obtener_por_id(id) {
    return ajax_service.get(`api/clientes/${id}`);
  }
};
