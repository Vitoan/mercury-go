import { ajax_service } from './ajax_service';
import { construir_query } from '@/utils/consulta';

export const clientes_service = {
  obtener_clientes(parametros = {}) {
    const query = construir_query(parametros);
    return ajax_service.get(`api/clientes${query}`);
  },

  obtener_por_id(id) {
    return ajax_service.get(`api/clientes/${id}`);
  },

  crear(cliente) {
    return ajax_service.post('api/clientes', cliente);
  },

  actualizar(id, cliente) {
    return ajax_service.put(`api/clientes/${id}`, cliente);
  },

  eliminar(id) {
    return ajax_service.delete(`api/clientes/${id}`);
  }
};
