import { E2E } from "./entorno";

const base = `${E2E.emuladorUrl}/identitytoolkit.googleapis.com/v1`;
// El emulador acepta "owner" como credencial de administrador.
const comoAdmin = { "Content-Type": "application/json", Authorization: "Bearer owner" };

async function pedir<T>(url: string, init: RequestInit): Promise<T> {
  const respuesta = await fetch(url, init);
  if (!respuesta.ok) {
    throw new Error(`Emulador de Firebase: ${respuesta.status} ${await respuesta.text()}`);
  }
  return (await respuesta.json()) as T;
}

/** Borra todas las cuentas del emulador. */
export async function borrarCuentas(): Promise<void> {
  await pedir(`${E2E.emuladorUrl}/emulator/v1/projects/${E2E.firebaseProjectId}/accounts`, { method: "DELETE" });
}

/** Crea una cuenta de email y contraseña, opcionalmente con el email ya verificado. */
export async function crearCuenta(email: string, password: string, emailVerificado: boolean): Promise<void> {
  const { localId } = await pedir<{ localId: string }>(`${base}/accounts:signUp?key=demo-api-key`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ email, password, returnSecureToken: true }),
  });

  if (emailVerificado) {
    await pedir(`${base}/projects/${E2E.firebaseProjectId}/accounts:update`, {
      method: "POST",
      headers: comoAdmin,
      body: JSON.stringify({ localId, emailVerified: true }),
    });
  }
}
