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

  crear(pedido, idempotency_key = null) {
    // Los precios NUNCA viajan desde el teléfono: el servidor lee los precios vigentes en MySQL
    const headers = idempotency_key ? { 'Idempotency-Key': String(idempotency_key) } : {};
    return ajax_service.post('api/pedidos', pedido, { headers });
  },

  cambiar_estado(id, estado, latitud = null, longitud = null, idempotency_key = null) {
    const payload = { estado };
    if (latitud !== null && longitud !== null) {
      payload.latitud = latitud;
      payload.longitud = longitud;
    }
    const opciones = idempotency_key ? { idempotency_key } : {};
    return ajax_service.put(`api/pedidos/${id}/estado`, payload, opciones);
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

  actualizar_picking(id, items, idempotency_key = null) {
    const opciones = idempotency_key ? { idempotency_key } : {};
    return ajax_service.put(`api/pedidos/${id}/picking`, { items }, opciones);
  },

  actualizar_pedido(id, datos) {
    return ajax_service.put(`api/pedidos/${id}`, datos);
  },

  registrar_cobro_chofer(id, monto, metodo = 'Efectivo', idempotency_key = null) {
    const opciones = idempotency_key ? { idempotency_key } : {};
    return ajax_service.post(`api/pedidos/${id}/cobro-chofer`, { monto, metodo }, opciones);
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
  },

  confirmar(id) {
    return ajax_service.post(`api/pedidos/${id}/confirmar`);
  },

  solicitar_cancelacion(id, motivo) {
    return ajax_service.post(`api/pedidos/${id}/solicitar-cancelacion`, { motivo });
  },

  resolver_cancelacion(id, aprobada, motivo = null) {
    return ajax_service.post(`api/pedidos/${id}/resolver-cancelacion`, { aprobada, motivo });
  },

  asignar_chofer(id, chofer_id) {
    return ajax_service.put(`api/pedidos/${id}/chofer`, { chofer_id });
  },

  registrar_no_entregado(id, motivo, nota = null, foto_url = null, idempotency_key = null) {
    const opciones = idempotency_key ? { idempotency_key } : {};
    return ajax_service.post(`api/pedidos/${id}/no-entregado`, { motivo, nota, foto_url }, opciones);
  },

  reintentar_entrega(id) {
    return ajax_service.post(`api/pedidos/${id}/reintentar-entrega`);
  },

  registrar_entrega_parcial(id, lineas, motivo = null, latitud = null, longitud = null, foto_url = null, firma_url = null, idempotency_key = null) {
    const opciones = idempotency_key ? { idempotency_key } : {};
    return ajax_service.post(`api/pedidos/${id}/entrega-parcial`, {
      lineas,
      motivo,
      latitud,
      longitud,
      foto_url,
      firma_url
    }, opciones);
  },

  ajustar_cobro(cobro_id, nuevo_monto, motivo) {
    return ajax_service.put(`api/pedidos/cobros/${cobro_id}/ajustar`, { nuevo_monto, motivo });
  }
};
