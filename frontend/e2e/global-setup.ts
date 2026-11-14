import { borrarCuentas, crearCuenta } from "./emulador";
import { E2E } from "./entorno";

/**
 * Antes de los E2E (Docker ya levantado por `npm run test:e2e`):
 * deja el emulador de Firebase vacío y crea al administrador con su email verificado.
 */
export default async function globalSetup() {
  await borrarCuentas();
  await crearCuenta(E2E.admin.email, E2E.admin.password, true);
}
