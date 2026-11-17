import { expect, test, type Page } from "@playwright/test";
import { pdfConTexto } from "./pdf";
import { ingresarComoAdministrador, ingresarComoEstudianteNuevo } from "./sesiones";

/** La Secretaría carga un reglamento con una palabra única (así la búsqueda encuentra este y no otros) y espera a que se indexe. */
async function cargarReglamento(page: Page, marca: string) {
  const titulo = `Reglamento ${marca}`;
  await ingresarComoAdministrador(page);
  await page.goto("/admin/documentos");
  const formulario = page.getByRole("form", { name: "Cargar documento" });
  await formulario.getByLabel(/^Archivo PDF/).setInputFiles({
    name: "reglamento.pdf",
    mimeType: "application/pdf",
    buffer: pdfConTexto(["CAPITULO I", "DE LAS MESAS DE EXAMEN", `La inscripcion a mesas ${marca} cierra 48 horas antes del examen.`]),
  });
  await formulario.getByLabel("Título").fill(titulo);
  await formulario.getByLabel("Fuente (opcional)").fill("Resolucion 4043/09");
  await formulario.getByRole("button", { name: "Cargar" }).click();
  await expect(page.getByRole("article", { name: titulo }).getByRole("status", { name: "Estado: Indexado" })).toBeVisible({ timeout: 30_000 });
  await page.getByRole("button", { name: "Cerrar sesión" }).click();
  await expect(page).toHaveURL(/\/login$/);
  return titulo;
}

test.describe("Asistente", () => {
  test("responde una pregunta sobre un reglamento cargado mostrando la fuente, y se abstiene si no hay evidencia", async ({ page }) => {
    const marca = `zq${Math.random().toString(36).slice(2, 8)}`;
    const titulo = await cargarReglamento(page, marca);

    await ingresarComoEstudianteNuevo(page);
    await page.getByRole("navigation", { name: "Principal" }).getByRole("link", { name: "Asistente" }).click();
    await expect(page).toHaveURL(/\/asistente$/);
    await expect(page.getByRole("note", { name: "Antes de preguntar" })).toContainText("se borran a los 30 días sin uso");

    // Pregunta con respuesta en los documentos: se ve la respuesta con su cita y la fuente.
    const formulario = page.getByRole("form", { name: "Hacer una pregunta" });
    await formulario.getByLabel("Tu pregunta").fill(`¿Cuándo cierra la inscripción a mesas ${marca}?`);
    await formulario.getByRole("button", { name: "Enviar" }).click();

    const mensajes = page.getByRole("log", { name: "Mensajes" });
    const respuesta = mensajes.getByRole("listitem", { name: "Respuesta del asistente" }).first();
    await expect(respuesta).toContainText("cierra 48 horas antes del examen");
    await expect(respuesta.getByRole("link", { name: "Fuente 1" })).toBeVisible();
    const fuentes = respuesta.getByRole("list", { name: "Fuentes" });
    await expect(fuentes).toContainText(`[1] ${titulo} (versión 1)`);
    await expect(fuentes).toContainText("CAPITULO I — DE LAS MESAS DE EXAMEN");
    await expect(fuentes).toContainText("Resolucion 4043/09");

    // El estudiante puede abrir el PDF citado.
    const descarga = page.waitForEvent("download");
    await fuentes.getByRole("button", { name: `Descargar el PDF de ${titulo}` }).click();
    expect((await descarga).suggestedFilename()).toBe(`${titulo} (v1).pdf`);

    // Pregunta sin respuesta en los documentos (en una conversación nueva): lo reconoce y deriva a Secretaría, sin inventar fuentes.
    const conversaciones = page.getByRole("navigation", { name: "Conversaciones" });
    await conversaciones.getByRole("button", { name: "Nueva" }).click();
    await expect(mensajes.getByRole("listitem")).toHaveCount(0);
    await formulario.getByLabel("Tu pregunta").fill("¿Quién ganó el torneo de ajedrez del barrio?");
    await formulario.getByRole("button", { name: "Enviar" }).click();
    const abstencion = mensajes.getByRole("listitem", { name: "Respuesta del asistente" });
    await expect(abstencion).toContainText("Sin información confirmada");
    await expect(abstencion).toContainText("consultalo en Secretaría");
    await expect(abstencion.getByRole("list", { name: "Fuentes" })).toHaveCount(0);

    // Historial mínimo: al volver, las dos conversaciones siguen ahí con sus mensajes.
    await page.reload();
    await expect(conversaciones.getByRole("listitem")).toHaveCount(2);
    await conversaciones.getByRole("button", { name: `¿Cuándo cierra la inscripción a mesas ${marca}?` }).click();
    await expect(mensajes.getByRole("listitem", { name: "Tu pregunta" })).toHaveText(`¿Cuándo cierra la inscripción a mesas ${marca}?`);
    await expect(mensajes.getByRole("listitem", { name: "Respuesta del asistente" }).getByRole("list", { name: "Fuentes" })).toContainText(titulo);
  });

  test("oculta el DNI de la pregunta y permite borrar la conversación", async ({ page }) => {
    await ingresarComoEstudianteNuevo(page);
    await page.goto("/asistente");

    const formulario = page.getByRole("form", { name: "Hacer una pregunta" });
    await formulario.getByLabel("Tu pregunta").fill("Mi DNI es 35123456, ¿puedo rendir libre?");
    await formulario.getByLabel("Tu pregunta").press("Enter");

    const mensajes = page.getByRole("log", { name: "Mensajes" });
    await expect(mensajes.getByRole("listitem", { name: "Tu pregunta" })).toHaveText("Mi DNI es [DNI], ¿puedo rendir libre?");
    await expect(mensajes.getByRole("listitem", { name: "Respuesta del asistente" })).toBeVisible();

    await page.getByRole("button", { name: "Borrar conversación" }).click();
    await page.getByRole("button", { name: "Sí, borrar" }).click();
    await expect(mensajes.getByRole("listitem")).toHaveCount(0);
    await expect(page.getByRole("navigation", { name: "Conversaciones" })).toContainText("Todavía no hiciste ninguna pregunta.");
  });
});
