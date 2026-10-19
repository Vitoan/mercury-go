import { escanear_qr } from '@/services/qr_service';
import { vibrar_toque, vibrar_exito, vibrar_error } from '@/services/vibracion_service';
import { pedidos_store } from '@/stores/pedidos_store';

export function useEscaneoQr(opciones = {}) {
  const { busquedaRef, estadoSeleccionadoRef, alEncontrarPedido, toast } = opciones;

  const escanearRemitoQr = async () => {
    vibrar_toque();
    const res = await escanear_qr();
    if (res.ok && res.contenido) {
      await vibrar_exito();
      const codigo = res.contenido.trim();
      if (busquedaRef) busquedaRef.value = codigo;
      if (estadoSeleccionadoRef) estadoSeleccionadoRef.value = '';
      pedidos_store.filtros.estados = '';
      await pedidos_store.establecer_busqueda(codigo);

      const encontrado = pedidos_store.pedidos.find(
        (p) => p.numero?.toLowerCase() === codigo.toLowerCase()
      ) || pedidos_store.pedidos[0];

      if (encontrado) {
        if (alEncontrarPedido) alEncontrarPedido(encontrado);
        if (toast) toast.mostrarExito(`Orden ${encontrado.numero} localizada.`);
      } else {
        if (toast) toast.mostrarExito(`No se encontró la orden para el código: ${codigo}`);
      }
    } else if (res.mensaje) {
      await vibrar_error();
      if (toast) toast.mostrarAdvertencia(res.mensaje);
    }
  };

  return {
    escanearRemitoQr
  };
}
