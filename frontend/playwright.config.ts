import { defineConfig, devices } from "@playwright/test";
import { E2E } from "./e2e/entorno";

// Sin esto, el modo sin conexión de Playwright no corta la red del service worker (lo necesita pwa.spec.ts).
process.env.PW_EXPERIMENTAL_SERVICE_WORKER_NETWORK_EVENTS = "1";

/**
 * Los E2E levantan el sistema completo:
 *  - PostgreSQL y Firebase Auth Emulator en Docker (los inicia global-setup.ts),
 *  - la API .NET en el ambiente E2E (base de datos recreada en cada corrida),
 *  - el frontend compilado en modo producción apuntando al emulador.
 */
export default defineConfig({
  testDir: "./e2e",
  globalSetup: "./e2e/global-setup.ts",
  fullyParallel: true,
  forbidOnly: !!process.env.CI,
  retries: process.env.CI ? 2 : 0,
  reporter: process.env.CI ? [["list"], ["html", { open: "never" }]] : "list",
  timeout: 60_000,
  // El primer inicio de sesión incluye el alta en la API; con Docker lento puede tardar varios segundos.
  expect: { timeout: 15_000 },
  use: {
    baseURL: E2E.webUrl,
    trace: "on-first-retry",
  },
  projects: [
    { name: "chromium", use: { ...devices["Desktop Chrome"] } },
    { name: "mobile", use: { ...devices["Pixel 7"] } },
  ],
  webServer: [
    {
      command: "dotnet run --project ../backend/src/Astrea.Api --no-launch-profile",
      url: `${E2E.apiUrl}/health/ready`,
      env: {
        ASPNETCORE_ENVIRONMENT: "E2E",
        ASPNETCORE_URLS: E2E.apiUrl,
        DOTNET_CLI_TELEMETRY_OPTOUT: "1",
      },
      reuseExistingServer: false,
      timeout: 300_000,
      stdout: "ignore",
      stderr: "pipe",
    },
    {
      // Build separado (.next-e2e) para no pisar el de desarrollo ni el de producción.
      command: `npx next build && npx next start --port ${E2E.webPort}`,
      url: E2E.webUrl,
      env: {
        NEXT_DIST_DIR: ".next-e2e",
        NEXT_PUBLIC_API_URL: `${E2E.apiUrl}/api/v1`,
        NEXT_PUBLIC_FIREBASE_API_KEY: "demo-api-key",
        NEXT_PUBLIC_FIREBASE_AUTH_DOMAIN: "localhost",
        NEXT_PUBLIC_FIREBASE_PROJECT_ID: E2E.firebaseProjectId,
        NEXT_PUBLIC_FIREBASE_AUTH_EMULATOR_URL: E2E.emuladorUrl,
      },
      reuseExistingServer: false,
      timeout: 300_000,
      stdout: "ignore",
      stderr: "pipe",
    },
  ],
});
