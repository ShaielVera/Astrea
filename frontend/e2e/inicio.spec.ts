import { expect, test } from "@playwright/test";

test.describe("Página de inicio", () => {
  test("carga y muestra el nombre del sistema", async ({ page }) => {
    await page.goto("/");

    await expect(page).toHaveTitle(/Astrea/);
    await expect(page.getByRole("heading", { level: 1, name: "Astrea" })).toBeVisible();
  });

  test("lista los módulos principales", async ({ page }) => {
    await page.goto("/");

    const modulos = page.getByRole("region", { name: "Módulos" }).getByRole("listitem");
    await expect(modulos).toHaveCount(8);
    for (const nombre of ["Mi perfil", "Plan de estudios", "Correlatividades", "Mesas de examen", "Mis trámites", "Mi cursada", "Hallazgos", "Asistente"]) {
      await expect(page.getByRole("heading", { level: 3, name: nombre })).toBeVisible();
    }
  });

  test("no tiene errores en la consola del navegador", async ({ page }) => {
    const errores: string[] = [];
    page.on("console", (mensaje) => {
      if (mensaje.type() === "error") errores.push(mensaje.text());
    });

    await page.goto("/");
    await page.waitForLoadState("networkidle");

    expect(errores).toEqual([]);
  });
});
