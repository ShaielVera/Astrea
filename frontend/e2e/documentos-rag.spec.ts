import { expect, test } from "@playwright/test";
import { ingresarComoAdministrador } from "./sesiones";
import { pdfConTexto } from "./pdf";

test.describe("Documentos del asistente", () => {
  test("la Secretaría carga un reglamento en PDF, se indexa y ve sus fragmentos", async ({ page }) => {
    const titulo = `Reglamento de mesas ${Math.random().toString(36).slice(2, 8)}`;
    await ingresarComoAdministrador(page);

    await page.getByRole("navigation", { name: "Secciones de administración" }).getByRole("link", { name: /Documentos del asistente/ }).click();
    await expect(page).toHaveURL(/\/admin\/documentos$/);

    const formulario = page.getByRole("form", { name: "Cargar documento" });
    await formulario.getByLabel(/^Archivo PDF/).setInputFiles({
      name: "reglamento.pdf",
      mimeType: "application/pdf",
      // La línea con el título hace único al archivo (Chromium y el celular corren contra la misma base).
      buffer: pdfConTexto(["CAPITULO I", "DE LAS MESAS DE EXAMEN", "La inscripcion a mesas cierra 48 horas antes del examen.", titulo]),
    });
    await formulario.getByLabel("Título").fill(titulo);
    await formulario.getByLabel("Fuente (opcional)").fill("Resolucion 4043/09");
    await formulario.getByRole("button", { name: "Cargar" }).click();
    await expect(page.getByRole("main").getByText(`Se cargó "${titulo}" (versión 1)`)).toBeVisible();

    // El procesador en segundo plano lo indexa; la página se actualiza sola.
    const tarjeta = page.getByRole("article", { name: titulo });
    await expect(tarjeta.getByRole("status", { name: "Estado: Indexado" })).toBeVisible({ timeout: 30_000 });
    await expect(tarjeta).toContainText("1 página, 1 fragmento");

    await tarjeta.getByRole("button", { name: "Ver fragmentos" }).click();
    const fragmentos = tarjeta.getByRole("list", { name: `Fragmentos de ${titulo}` });
    await expect(fragmentos).toContainText("cierra 48 horas antes");
    await expect(fragmentos).toContainText("CAPITULO I — DE LAS MESAS DE EXAMEN");
  });
});
