import { expect, test } from "@playwright/test";
import { E2E } from "./entorno";
import { ingresarComoEstudianteNuevo } from "./sesiones";

test.describe("Aplicación instalable (PWA) y endurecimiento", () => {
  test("publica el manifest con sus íconos y cabeceras de seguridad", async ({ request }) => {
    const manifest = await request.get("/manifest.webmanifest");
    expect(manifest.ok()).toBe(true);
    const contenido = await manifest.json();
    expect(contenido).toMatchObject({ short_name: "Astrea", display: "standalone", start_url: "/" });
    expect(contenido.icons.map((icono: { sizes: string }) => icono.sizes)).toEqual(expect.arrayContaining(["192x192", "512x512"]));
    for (const icono of contenido.icons as { src: string }[]) {
      const respuesta = await request.get(icono.src);
      expect(respuesta.headers()["content-type"]).toBe("image/png");
    }

    const inicio = await request.get("/");
    expect(inicio.headers()["x-frame-options"]).toBe("DENY");
    expect(inicio.headers()["x-content-type-options"]).toBe("nosniff");
    expect(inicio.headers()["x-powered-by"]).toBeUndefined();
  });

  test("sin conexión muestra una página fija y nunca guarda datos personales en el dispositivo", async ({ page, context }) => {
    await ingresarComoEstudianteNuevo(page);
    await page.evaluate(() => navigator.serviceWorker.ready);
    await page.reload();
    await expect.poll(() => page.evaluate(() => navigator.serviceWorker.controller !== null)).toBe(true);

    // Navega por secciones con datos personales mientras el service worker está activo.
    for (const ruta of ["/tramites", "/asistente", "/hallazgos"]) {
      await page.goto(ruta);
      await expect(page.getByRole("heading", { level: 1 })).toBeVisible();
    }

    const guardadas = await page.evaluate(async () => {
      const urls: string[] = [];
      for (const nombre of await caches.keys()) {
        const cache = await caches.open(nombre);
        urls.push(...(await cache.keys()).map((pedido) => pedido.url));
      }
      return urls;
    });
    expect(guardadas.length).toBeGreaterThan(0);
    for (const url of guardadas) {
      const { origin, pathname } = new URL(url);
      expect(origin, url).toBe(E2E.webUrl);
      expect(pathname === "/sin-conexion" || pathname.startsWith("/_next/static/") || pathname.startsWith("/icons/"), url).toBe(true);
    }

    await context.setOffline(true);
    // Una URL nunca visitada: no está en la caché HTTP del navegador, así que responde el service worker.
    // (Una ya visitada puede salir de esa caché: es solo la estructura de la página, sin datos, que avisa que no hay servidor.)
    await page.goto(`/tramites?visita=${Date.now()}`);
    await expect(page.getByRole("heading", { name: "Sin conexión" })).toBeVisible();
    await expect(page.getByText("por seguridad, no se guardan en el dispositivo")).toBeVisible();
    await context.setOffline(false);
  });

  test("con el teclado se puede saltar directo al contenido", async ({ page }) => {
    await page.goto("/");
    await page.keyboard.press("Tab");
    const salto = page.getByRole("link", { name: "Saltar al contenido" });
    await expect(salto).toBeFocused();
    await page.keyboard.press("Enter");
    await expect(page.locator("#contenido")).toBeFocused();
  });
});
