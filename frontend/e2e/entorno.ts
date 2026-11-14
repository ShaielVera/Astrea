/** Puertos y datos de prueba del ambiente E2E. Todo es ficticio y local. */
export const E2E = {
  webPort: 3100,
  webUrl: "http://localhost:3100",
  apiUrl: "http://localhost:5180",
  emuladorUrl: "http://127.0.0.1:9099",
  firebaseProjectId: "demo-astrea",
  /** Debe coincidir con Identidad:AdministradoresIniciales en appsettings.E2E.json. */
  admin: { email: "admin@astrea.test", password: "admin-e2e-astrea" },
  passwordEstudiantes: "estudiante-e2e-astrea",
} as const;

/** Email único por test para que los proyectos de Playwright no compartan cuentas. */
export function emailUnico(prefijo: string): string {
  return `${prefijo}-${Date.now()}-${Math.random().toString(36).slice(2, 8)}@astrea.test`;
}
