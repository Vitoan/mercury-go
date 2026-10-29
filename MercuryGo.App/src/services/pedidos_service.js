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

  cambiar_estado(id, estado, latitud = null, longitud = null, idempotency_key = null, ocurrido_en = null, es_offline = false) {
    const payload = { estado };
    if (latitud !== null && longitud !== null) {
      payload.latitud = latitud;
      payload.longitud = longitud;
    }
    if (ocurrido_en) payload.ocurrido_en = ocurrido_en;
    if (es_offline) payload.es_offline = true;
    const opciones = idempotency_key ? { idempotency_key } : {};
    return ajax_service.put(`api/pedidos/${id}/estado`, payload, opciones);
  },

  cancelar(id) {
    return ajax_service.delete(`api/pedidos/${id}`);
  },

  descargar_comprobante(id) {
    return ajax_service.blob(`/api/pedidos/${id}/comprobante`);
  },

  descargar_archivo_comprobante(id) {
    return ajax_service.blob(`/api/pedidos/${id}/comprobante-pago/archivo`);
  },

  descargar_archivo_pago_cuenta(id) {
    return ajax_service.blob(`/api/pedidos/cuenta-corriente/pagos/${id}/archivo`);
  },

  subir_comprobante_pago(id, archivo) {
    const formData = new FormData();
    formData.append('archivo', archivo);
    return ajax_service.post(`api/pedidos/${id}/comprobante-pago`, formData);
  },

  actualizar_picking(id, items, idempotency_key = null, es_offline = false) {
    const opciones = idempotency_key ? { idempotency_key } : {};
    const cuerpo = { items };
    if (es_offline) cuerpo.es_offline = true;
    return ajax_service.put(`api/pedidos/${id}/picking`, cuerpo, opciones);
  },

  actualizar_pedido(id, datos, idempotency_key = null) {
    const opciones = idempotency_key ? { idempotency_key } : {};
    return ajax_service.put(`api/pedidos/${id}`, datos, opciones);
  },

  registrar_cobro_chofer(id, monto, metodo = 'Efectivo', idempotency_key = null, ocurrido_en = null) {
    const opciones = idempotency_key ? { idempotency_key } : {};
    const cuerpo = { monto, metodo };
    if (ocurrido_en) cuerpo.ocurrido_en = ocurrido_en;
    return ajax_service.post(`api/pedidos/${id}/cobro-chofer`, cuerpo, opciones);
  },

  obtener_cuenta_corriente() {
    return ajax_service.get('api/pedidos/cuenta-corriente', { guardable: true });
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

  subir_pago_cuenta_corriente(monto, archivo = null, observaciones = '', cliente_id = null) {
    const formData = new FormData();
    formData.append('monto', monto);
    if (archivo) formData.append('archivo', archivo);
    if (observaciones) formData.append('observaciones', observaciones);
    if (cliente_id) formData.append('cliente_id', cliente_id);
    return ajax_service.post('api/pedidos/cuenta-corriente/pagos', formData);
  },

  listar_pagos_cuenta_corriente() {
    return ajax_service.get('api/pedidos/cuenta-corriente/pagos');
  },

  aprobar_pago_cuenta_corriente(id, monto = null) {
    const cuerpo = monto !== null && monto !== undefined ? { monto: Number(monto) } : null;
    return ajax_service.put(`api/pedidos/cuenta-corriente/pagos/${id}/aprobar`, cuerpo);
  },

  rechazar_pago_cuenta_corriente(id, motivo = '') {
    return ajax_service.put(`api/pedidos/cuenta-corriente/pagos/${id}/rechazar`, { motivo });
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

  obtener_choferes() {
    return ajax_service.get('api/pedidos/choferes');
  },

  registrar_no_entregado(id, motivo, nota = null, foto_url = null, idempotency_key = null, ocurrido_en = null, es_offline = false) {
    const opciones = idempotency_key ? { idempotency_key } : {};
    const cuerpo = { motivo, nota, foto_url };
    if (ocurrido_en) cuerpo.ocurrido_en = ocurrido_en;
    if (es_offline) cuerpo.es_offline = true;
    return ajax_service.post(`api/pedidos/${id}/no-entregado`, cuerpo, opciones);
  },

  reintentar_entrega(id) {
    return ajax_service.post(`api/pedidos/${id}/reintentar-entrega`);
  },

  registrar_entrega_parcial(id, items_rechazados, motivo = null, latitud = null, longitud = null, foto_url = null, firma_url = null, idempotency_key = null, ocurrido_en = null, es_offline = false) {
    const opciones = idempotency_key ? { idempotency_key } : {};
    const cuerpo = { items_rechazados, motivo, latitud, longitud, foto_url, firma_url };
    if (ocurrido_en) cuerpo.ocurrido_en = ocurrido_en;
    if (es_offline) cuerpo.es_offline = true;
    return ajax_service.post(`api/pedidos/${id}/entrega-parcial`, cuerpo, opciones);
  },

  ajustar_cobro(cobro_id, nuevo_monto, motivo) {
    return ajax_service.put(`api/pedidos/cobros/${cobro_id}/ajustar`, { nuevo_monto, motivo });
  }
};
