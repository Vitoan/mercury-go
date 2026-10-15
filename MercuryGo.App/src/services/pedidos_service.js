import { ajax_service } from './ajax_service';
import { construir_query } from '@/utils/consulta';

export const pedidos_service = {
  obtener_resumen() {
    return ajax_service.get('api/pedidos/resumen', { guardable: true });
  },

  obtener_pedidos(parametros = {}) {
    const query = construir_query(parametros);
    return ajax_service.get(`api/pedidos${query}`, { guardable: true });
  },

  obtener_por_id(id) {
    return ajax_service.get(`api/pedidos/${id}`, { guardable: true });
  },

  crear(pedido) {
    // Los precios NUNCA viajan desde el teléfono: el servidor lee los precios vigentes en MySQL
    return ajax_service.post('api/pedidos', pedido);
  },

  cambiar_estado(id, estado, latitud = null, longitud = null) {
    const payload = { estado };
    if (latitud !== null && longitud !== null) {
      payload.latitud = latitud;
      payload.longitud = longitud;
    }
    return ajax_service.put(`api/pedidos/${id}/estado`, payload);
  },

  cancelar(id) {
    return ajax_service.delete(`api/pedidos/${id}`);
  },

  descargar_comprobante(id) {
    return ajax_service.blob(`/api/pedidos/${id}/comprobante`);
  },

  obtener_qr_url(id) {
    return ajax_service.blob(`/api/pedidos/${id}/qr`);
  },

  subir_comprobante_pago(id, archivo) {
    const formData = new FormData();
    formData.append('archivo', archivo);
    return ajax_service.post(`api/pedidos/${id}/comprobante-pago`, formData);
  },

  actualizar_picking(id, items) {
    return ajax_service.put(`api/pedidos/${id}/picking`, { items });
  },

  actualizar_pedido(id, datos) {
    return ajax_service.put(`api/pedidos/${id}`, datos);
  },

  registrar_cobro_chofer(id, monto, metodo = 'Efectivo') {
    return ajax_service.post(`api/pedidos/${id}/cobro-chofer`, { monto, metodo });
  },

  obtener_cuenta_corriente() {
    return ajax_service.get('api/pedidos/cuenta-corriente');
  },

  obtener_cuentas_corrientes_clientes() {
    return ajax_service.get('api/pedidos/cuentas-corrientes-clientes');
  },

  aprobar_comprobante(id, monto = null) {
    const cuerpo = monto !== null && monto !== undefined ? { monto: Number(monto) } : null;
    return ajax_service.put(`api/pedidos/${id}/aprobar-comprobante`, cuerpo);
  },

  rechazar_comprobante(id, motivo = '') {
    return ajax_service.put(`api/pedidos/${id}/rechazar-comprobante`, { motivo });
  }
};
