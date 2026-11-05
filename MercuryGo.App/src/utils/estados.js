// Centralización visual de estados y roles del dominio logístico B2B de MercuryGO.
// Toda pantalla o modal lee de aquí para garantizar consistencia en colores, textos e iconos.
import {
  timeOutline,
  checkmarkCircleOutline,
  cubeOutline,
  carOutline,
  locationOutline,
  checkmarkDoneCircleOutline,
  alertCircleOutline,
  closeCircleOutline,
  informationCircleOutline,
  personOutline,
  buildOutline,
  businessOutline
} from 'ionicons/icons';

const ESTADOS_PEDIDO = {
  pendiente: { texto: 'Pendiente', color: 'medium', icono: timeOutline },
  confirmado: { texto: 'Confirmado', color: 'tertiary', icono: checkmarkCircleOutline },
  enpreparacion: { texto: 'En Depósito', color: 'warning', icono: cubeOutline },
  despachado: { texto: 'En Ruta', color: 'primary', icono: carOutline },
  enpuerta: { texto: 'En Puerta', color: 'secondary', icono: locationOutline },
  entregado: { texto: 'Entregado', color: 'success', icono: checkmarkDoneCircleOutline },
  noentregado: { texto: 'No Entregado', color: 'danger', icono: alertCircleOutline },
  cancelado: { texto: 'Cancelado', color: 'danger', icono: closeCircleOutline }
};

// Siguiente estado habitual en la secuencia logística (atajo de UI para operarios y choferes)
const SIGUIENTE_ESTADO_PEDIDO = {
  pendiente: 'Confirmado',
  confirmado: 'EnPreparacion',
  enpreparacion: 'Despachado',
  despachado: 'EnPuerta',
  enpuerta: 'Entregado'
};

const ROLES_USUARIO = {
  admin: { texto: 'Administrador', color: 'primary', icono: personOutline },
  operario: { texto: 'Operario Almacén', color: 'warning', icono: buildOutline },
  chofer: { texto: 'Chofer Reparto', color: 'success', icono: carOutline },
  cliente: { texto: 'Cliente Comercial', color: 'tertiary', icono: businessOutline }
};

const ESTADOS_COMPROBANTE = {
  pendiente: { texto: 'Comprobante Pendiente', color: 'warning', icono: timeOutline },
  subido: { texto: 'En Revisión', color: 'warning', icono: timeOutline },
  aprobado: { texto: 'Acreditado', color: 'success', icono: checkmarkCircleOutline },
  acreditado: { texto: 'Acreditado', color: 'success', icono: checkmarkDoneCircleOutline },
  rechazado: { texto: 'Rechazado', color: 'danger', icono: closeCircleOutline }
};

function normalizarClave(clave) {
  return String(clave || '')
    .toLowerCase()
    .replace(/[\s_-]+/g, '');
}

function leer(tabla, clave) {
  const normalizada = normalizarClave(clave);
  return (
    tabla[normalizada] || {
      texto: String(clave || 'Desconocido'),
      color: 'medium',
      icono: informationCircleOutline
    }
  );
}

export function estado_pedido(estado) {
  return leer(ESTADOS_PEDIDO, estado);
}

export function color_estado_pedido(estado) {
  return estado_pedido(estado).color;
}

export function texto_estado_pedido(estado) {
  return estado_pedido(estado).texto;
}

export function icono_estado_pedido(estado) {
  return estado_pedido(estado).icono;
}

export function siguiente_estado_pedido(estado) {
  const norm = normalizarClave(estado);
  return SIGUIENTE_ESTADO_PEDIDO[norm] || '';
}

export function rol_usuario(rol) {
  return leer(ROLES_USUARIO, rol);
}

export function estado_comprobante(estado) {
  return leer(ESTADOS_COMPROBANTE, estado);
}

export function clase_tinte(color) {
  return `mercury-tinte-${color || 'medium'}`;
}
