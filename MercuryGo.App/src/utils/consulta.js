// Construye el query string de un endpoint a partir de un objeto de parámetros, omitiendo valores vacíos.
// Evita concatenar a mano y previene bugs como '?busqueda=undefined' o errores de codificación URI.
export function construir_query(parametros = {}) {
  const query = new URLSearchParams();
  Object.keys(parametros).forEach((clave) => {
    const valor = parametros[clave];
    // null, undefined y '' significan 'sin filtro' y no viajan.
    // El booleano false sí viaja (ej: disponible=false).
    if (valor === null || valor === undefined || valor === '') return;
    query.set(clave, valor);
  });
  const texto = query.toString();
  return texto ? `?${texto}` : '';
}
