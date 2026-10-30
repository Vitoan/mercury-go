// Utilidades centralizadas de formato para MercuryGO (moneda, fecha y hora en es-AR)

export function formatearMoneda(valor, conDecimales = false) {
  const num = Number(valor);
  if (Number.isNaN(num) || valor === null || valor === undefined) return '$0';
  if (conDecimales) {
    return '$' + num.toLocaleString('es-AR', {
      minimumFractionDigits: 2,
      maximumFractionDigits: 2
    });
  }
  return '$' + num.toLocaleString('es-AR');
}

export function formatearFechaHora(fechaStr) {
  if (!fechaStr) return '';
  const d = new Date(fechaStr);
  return Number.isNaN(d.getTime()) ? '' : d.toLocaleString('es-AR', {
    day: '2-digit',
    month: '2-digit',
    year: 'numeric',
    hour: '2-digit',
    minute: '2-digit'
  });
}
