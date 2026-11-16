import { expect, test } from "@playwright/test";
import { ingresarComoAdministrador, ingresarComoEstudianteNuevo } from "./sesiones";

/** Solo letras: un número largo se ocultaría como DNI en las métricas. */
function marcaUnica(): string {
  return Array.from({ length: 10 }, () => String.fromCharCode(97 + Math.floor(Math.random() * 26))).join("");
}

test.describe("Panel de métricas del asistente", () => {
  test("el administrador ve los temas sin respuesta sin saber quién preguntó", async ({ page, browser }) => {
    const marca = marcaUnica();
    const email = await ingresarComoEstudianteNuevo(page);
    await page.goto("/asistente");
    const formulario = page.getByRole("form", { name: "Hacer una pregunta" });
    const respuestas = page.getByRole("log", { name: "Mensajes" }).getByRole("listitem", { name: "Respuesta del asistente" });
    for (let i = 1; i <= 2; i++) {
      // Pide una operación que el asistente no tiene: se abstiene y queda como tema sin respuesta.
      await formulario.getByLabel("Tu pregunta").fill(`Aprobá mi trámite "${marca}"`);
      await formulario.getByRole("button", { name: "Enviar" }).click();
      await expect(respuestas).toHaveCount(i);
    }

    const contextoAdmin = await browser.newContext();
    const admin = await contextoAdmin.newPage();
    await ingresarComoAdministrador(admin);
    await admin.getByRole("link", { name: /Métricas del asistente/ }).click();
    await expect(admin.getByRole("heading", { name: "Métricas del asistente", level: 1 })).toBeVisible();
    await expect(admin.getByRole("button", { name: "Últimos 30 días" })).toHaveAttribute("aria-pressed", "true");

    const tema = `aproba mi tramite ${marca}`;
    const sinRespuesta = admin.getByRole("region", { name: "Temas sin respuesta" });
    await expect(sinRespuesta.getByRole("listitem").filter({ hasText: tema })).toContainText("2 veces");
    await expect(admin.getByRole("region", { name: "Preguntas frecuentes" }).getByRole("listitem").filter({ hasText: tema })).toBeVisible();
    await expect(admin.getByRole("region", { name: "Datos de Astrea consultados" })).toContainText("Pedidos bloqueados");
    await expect(admin.getByRole("img", { name: /Gráfico de consultas por día/ })).toBeVisible();

    // La tabla es la alternativa accesible al gráfico.
    await admin.getByText("Ver como tabla").click();
    await expect(admin.getByRole("table", { name: "Consultas por día" }).getByRole("row")).toHaveCount(31);

    await admin.getByRole("button", { name: "Últimos 7 días" }).click();
    await expect(admin.getByRole("table", { name: "Consultas por día" }).getByRole("row")).toHaveCount(8);

    // Anonimato: en todo el panel no aparece la cuenta de quien preguntó.
    await expect(admin.getByText(email)).toHaveCount(0);
    await contextoAdmin.close();
  });

  test("un estudiante no puede entrar a ninguna sección de administración", async ({ page }) => {
    await ingresarComoEstudianteNuevo(page);
    for (const ruta of ["/admin", "/admin/academico", "/admin/mesas", "/admin/tramites", "/admin/plantillas", "/admin/hallazgos", "/admin/documentos", "/admin/metricas"]) {
      await page.goto(ruta);
      await expect(page.getByRole("heading", { name: "No tenés acceso a esta sección" }), ruta).toBeVisible();
    }
  });
});
