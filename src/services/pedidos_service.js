import { ajax_service } from './ajax_service';
import { construir_query } from '@/utils/consulta';

export const pedidos_service = {
  obtener_resumen() {
    return ajax_service.get('api/pedidos/resumen');
  },

  obtener_pedidos(parametros = {}) {
    const query = construir_query(parametros);
    return ajax_service.get(`api/pedidos${query}`);
  },

  obtener_por_id(id) {
    return ajax_service.get(`api/pedidos/${id}`);
  },

  crear(pedido) {
    // Los precios NUNCA viajan desde el teléfono: el servidor lee los precios vigentes en MySQL
    return ajax_service.post('api/pedidos', pedido);
  },

  cambiar_estado(id, estado) {
    return ajax_service.put(`api/pedidos/${id}/estado`, { estado });
  },

  cancelar(id) {
    return ajax_service.delete(`api/pedidos/${id}`);
  },

  descargar_comprobante(id) {
    return ajax_service.blob(`api/pedidos/${id}/comprobante`);
  },

  obtener_qr_url(id) {
    return ajax_service.blob(`api/pedidos/${id}/qr`);
  },

  subir_comprobante_pago(id, archivo) {
    const formData = new FormData();
    formData.append('archivo', archivo);
    return ajax_service.post(`api/pedidos/${id}/comprobante-pago`, formData);
  }
};
