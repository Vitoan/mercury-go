import { BiometricAuth } from '@aparajita/capacitor-biometric-auth';

// La biometría NO reemplaza al login con contraseña:
// lo que hace es desbloquear la sesión ya persistida en el SecureStorage del dispositivo.
// Si no hay sesión previa, no hay nada que desbloquear.

export async function biometria_disponible() {
  try {
    const resultado = await BiometricAuth.checkBiometry();
    return Boolean(resultado?.isAvailable);
  } catch {
    // En navegador web el plugin no corre; se retorna false de manera limpia
    return false;
  }
}

export async function nombre_biometria() {
  try {
    const resultado = await BiometricAuth.checkBiometry();
    return resultado?.biometryType ? String(resultado.biometryType) : '';
  } catch {
    return '';
  }
}

export async function verificar_identidad(motivo = 'Confirmá tu identidad para acceder a MercuryGO') {
  try {
    await BiometricAuth.authenticate({
      reason: motivo,
      cancelTitle: 'Cancelar',
      allowDeviceCredential: true,
      androidTitle: 'MercuryGO Logística',
      androidSubtitle: motivo,
      androidConfirmationRequired: false
    });
    return true;
  } catch {
    return false;
  }
}
