import { ajax_service } from './ajax_service';

export const productos_service = {
  obtener_resumen() {
    return ajax_service.get('api/productos/resumen');
  },

  obtener_por_id(id) {
    return ajax_service.get(`api/productos/${id}`);
  }
};
