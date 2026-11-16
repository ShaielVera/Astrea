import { expect, test } from "@playwright/test";
import { ingresarComoEstudianteNuevo } from "./sesiones";

// La base E2E tiene el catálogo real del instituto: "Programación II" (Desarrollo de Software) pide "Programación I" aprobada para rendir.
test.describe("Asistente con datos de Astrea", () => {
  test("consulta correlatividades actuales y propone un trámite que se crea una sola vez al confirmar", async ({ page }) => {
    await ingresarComoEstudianteNuevo(page);
    await page.goto("/asistente");
    const formulario = page.getByRole("form", { name: "Hacer una pregunta" });
    const mensajes = page.getByRole("log", { name: "Mensajes" });

    // Datos vivos: la respuesta cita la sección de Astrea de donde salen.
    await formulario.getByLabel("Tu pregunta").fill('¿Qué correlativas tiene "Programación II"?');
    await formulario.getByRole("button", { name: "Enviar" }).click();
    const respuesta = mensajes.getByRole("listitem", { name: "Respuesta del asistente" }).first();
    await expect(respuesta).toContainText("Programación I aprobada");
    const fuentes = respuesta.getByRole("list", { name: "Fuentes" });
    await expect(fuentes).toContainText("[1] Correlatividades de Programación II");
    await expect(fuentes).toContainText("Datos actuales de Astrea");
    await expect(fuentes.getByRole("link", { name: "Ver en Correlatividades de Programación II" })).toHaveAttribute("href", "/correlatividades");

    // Propuesta de trámite: no se envía hasta confirmar.
    await formulario.getByLabel("Tu pregunta").fill('Quiero una constancia de alumno regular de "Desarrollo de Software" para "ANSES"');
    await formulario.getByRole("button", { name: "Enviar" }).click();
    const solicitud = mensajes.getByRole("region", { name: "Solicitud: Constancia de alumno regular" });
    await expect(solicitud).toContainText("Tecnicatura Superior en Desarrollo de Software");
    await expect(solicitud).toContainText("ANSES");
    await expect(solicitud).toContainText("Todavía no se envió");

    // Doble clic en confirmar: se crea un solo trámite.
    await solicitud.getByRole("button", { name: "Confirmar y enviar" }).dblclick();
    await expect(solicitud.getByRole("status")).toContainText(/Enviada: trámite T-\d+/);
    await expect(solicitud.getByRole("button", { name: "Confirmar y enviar" })).toHaveCount(0);

    await solicitud.getByRole("link", { name: "Seguilo en Mis trámites" }).click();
    await expect(page).toHaveURL(/\/tramites$/);
    const pedidos = page.getByRole("region", { name: "Tus pedidos" }).getByRole("listitem");
    await expect(pedidos).toHaveCount(1);
    await expect(pedidos.first()).toContainText("Constancia de alumno regular");
  });

  test("si falta un dato lo pregunta, y una solicitud cancelada no se envía", async ({ page }) => {
    await ingresarComoEstudianteNuevo(page);
    await page.goto("/asistente");
    const formulario = page.getByRole("form", { name: "Hacer una pregunta" });
    const mensajes = page.getByRole("log", { name: "Mensajes" });

    await formulario.getByLabel("Tu pregunta").fill("Quiero una constancia de alumno regular");
    await formulario.getByRole("button", { name: "Enviar" }).click();
    await expect(mensajes.getByRole("listitem", { name: "Respuesta del asistente" })).toHaveText("¿De qué carrera sos?");

    await formulario.getByLabel("Tu pregunta").fill('De "Desarrollo de Software", quiero una constancia de alumno regular para "mi trabajo"');
    await formulario.getByRole("button", { name: "Enviar" }).click();
    const solicitud = mensajes.getByRole("region", { name: "Solicitud: Constancia de alumno regular" });
    await solicitud.getByRole("button", { name: "Cancelar" }).click();
    await expect(solicitud.getByRole("status")).toHaveText("Cancelaste esta solicitud. No se envió nada.");

    await page.goto("/tramites");
    await expect(page.getByText("Todavía no pediste ningún trámite.")).toBeVisible();
  });
});
