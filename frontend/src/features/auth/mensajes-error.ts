import { FirebaseError } from "firebase/app";
import { ApiError } from "@/lib/api-client";

const MENSAJES_FIREBASE: Record<string, string> = {
  "auth/invalid-credential": "El email o la contraseña no son correctos.",
  // El emulador (y proyectos sin protección contra enumeración de emails) usan estos códigos.
  "auth/wrong-password": "El email o la contraseña no son correctos.",
  "auth/user-not-found": "El email o la contraseña no son correctos.",
  "auth/invalid-email": "El email no tiene un formato válido.",
  "auth/user-disabled": "La cuenta está deshabilitada.",
  "auth/email-already-in-use": "Ya existe una cuenta con ese email. Probá iniciar sesión.",
  "auth/weak-password": "La contraseña debe tener al menos 6 caracteres.",
  "auth/too-many-requests": "Demasiados intentos. Esperá unos minutos y volvé a probar.",
  "auth/popup-closed-by-user": "Se cerró la ventana de Google antes de terminar.",
  "auth/network-request-failed": "No hay conexión. Revisá tu internet.",
};

export function mensajeDeError(error: unknown): string {
  if (error instanceof FirebaseError) {
    return MENSAJES_FIREBASE[error.code] ?? "No se pudo iniciar sesión. Intentá de nuevo.";
  }
  if (error instanceof ApiError) {
    // El límite de uso responde sin cuerpo: se explica acá.
    if (error.status === 429) return "Hiciste muchos pedidos seguidos. Esperá un minuto y volvé a probar.";
    return error.message;
  }
  return "Ocurrió un error inesperado. Intentá de nuevo.";
}
