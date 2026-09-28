import { ajax_service } from './ajax_service';
import { construir_query } from '@/utils/consulta';

export const productos_service = {
  obtener_resumen() {
    return ajax_service.get('api/productos/resumen');
  },

  obtener_listado(parametros = {}) {
    const query = construir_query(parametros);
    return ajax_service.get(`api/productos/listado${query}`);
  },

  obtener_por_id(id) {
    return ajax_service.get(`api/productos/${id}`);
  },

  crear(producto) {
    return ajax_service.post('api/productos', producto);
  },

  actualizar(id, producto) {
    return ajax_service.put(`api/productos/${id}`, producto);
  },

  eliminar(id) {
    return ajax_service.delete(`api/productos/${id}`);
  },

  subir_imagen(id, archivo) {
    const formData = new FormData();
    formData.append('archivo', archivo);
    return ajax_service.post(`api/productos/${id}/imagen`, formData);
  }
};
