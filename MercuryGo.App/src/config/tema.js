import { StatusBar, Style } from '@capacitor/status-bar';

const CLAVE_TEMA = 'mercurygo_tema';

export function es_tema_oscuro() {
  const guardado = localStorage.getItem(CLAVE_TEMA);
  if (guardado !== null) {
    return guardado === 'oscuro';
  }
  return window.matchMedia && window.matchMedia('(prefers-color-scheme: dark)').matches;
}

export async function sincronizar_status_bar(oscuro) {
  try {
    await StatusBar.setStyle({ style: oscuro ? Style.Dark : Style.Light });
    await StatusBar.setBackgroundColor({ color: oscuro ? '#090d16' : '#f1f5f9' });
  } catch {
    // Si corre en web o el plugin no está disponible, se ignora silenciosamente
  }
}

export function aplicar_tema_guardado() {
  const oscuro = es_tema_oscuro();
  document.documentElement.classList.toggle('ion-palette-dark', oscuro);
  document.documentElement.style.colorScheme = oscuro ? 'dark' : 'light';
  sincronizar_status_bar(oscuro);
}

export function alternar_tema() {
  const oscuro = !es_tema_oscuro();
  localStorage.setItem(CLAVE_TEMA, oscuro ? 'oscuro' : 'claro');
  document.documentElement.classList.toggle('ion-palette-dark', oscuro);
  document.documentElement.style.colorScheme = oscuro ? 'dark' : 'light';
  sincronizar_status_bar(oscuro);
  return oscuro;
}

export function esta_oscuro() {
  return document.documentElement.classList.contains('ion-palette-dark');
}