const CLAVE_TEMA = 'mercurygo_tema';

export function es_tema_oscuro() {
  const guardado = localStorage.getItem(CLAVE_TEMA);
  if (guardado !== null) {
    return guardado === 'oscuro';
  }
  return window.matchMedia && window.matchMedia('(prefers-color-scheme: dark)').matches;
}

export function aplicar_tema_guardado() {
  document.documentElement.classList.toggle('ion-palette-dark', es_tema_oscuro());
}

export function alternar_tema() {
  const oscuro = !es_tema_oscuro();
  localStorage.setItem(CLAVE_TEMA, oscuro ? 'oscuro' : 'claro');
  document.documentElement.classList.toggle('ion-palette-dark', oscuro);
  return oscuro;
}

export function esta_oscuro() {
  return document.documentElement.classList.contains('ion-palette-dark');
}