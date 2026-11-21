import { dniUnico } from "./entorno";
import { expect, test, type Browser, type Page } from "@playwright/test";
import { completarPerfil, ingresarComoAdministrador, ingresarComoEstudianteNuevo } from "./sesiones";

const TSDS = "Tecnicatura Superior en Desarrollo de Software";

async function sesiones(browser: Browser): Promise<{ estudiante: Page; admin: Page }> {
  const estudiante = await (await browser.newContext()).newPage();
  const admin = await (await browser.newContext()).newPage();
  await ingresarComoEstudianteNuevo(estudiante);
  await completarPerfil(estudiante);
  await ingresarComoAdministrador(admin);
  return { estudiante, admin };
}

/** El estudiante elige el tipo, completa el formulario y devuelve el número asignado (T-000123). */
async function pedir(estudiante: Page, tipo: string, completar: (formulario: ReturnType<Page["getByRole"]>) => Promise<void>): Promise<string> {
  await estudiante.getByRole("navigation", { name: "Principal" }).getByRole("link", { name: "Mis trámites" }).click();
  await expect(estudiante).toHaveURL(/\/tramites$/);
  await estudiante.getByRole("combobox", { name: /^Tipo de trámite/ }).selectOption({ label: tipo });
  const formulario = estudiante.getByRole("form", { name: tipo });
  await formulario.getByRole("combobox", { name: /^Carrera/ }).selectOption({ label: TSDS });
  await completar(formulario);
  await formulario.getByRole("button", { name: "Enviar solicitud" }).click();

  const aviso = estudiante.getByRole("status");
  await expect(aviso).toContainText(`pediste ${tipo}`);
  const numero = (await aviso.textContent())!.match(/T-\d{6}/)![0];
  const detalle = estudiante.getByRole("region", { name: `Trámite ${numero}` });
  await expect(detalle).toContainText("Pendiente");
  await expect(detalle.getByRole("list", { name: "Historial" }).getByRole("listitem")).toHaveCount(1);
  return numero;
}

/** El administrador busca el trámite por número en la bandeja y lo abre. */
async function abrirEnBandeja(admin: Page, numero: string) {
  await admin.goto("/admin");
  await admin.getByRole("navigation", { name: "Secciones de administración" }).getByRole("link", { name: /Trámites/ }).click();
  await expect(admin).toHaveURL(/\/admin\/tramites$/);
  await admin.getByRole("searchbox", { name: /^Número, email o apellido/ }).fill(numero);
  await admin.getByRole("form", { name: "Filtrar trámites" }).getByRole("button", { name: "Buscar" }).click();
  await admin.getByRole("button", { name: new RegExp(numero) }).click();
  return admin.getByRole("region", { name: `Trámite ${numero}` });
}

/** El estudiante vuelve a "Mis trámites" y abre su pedido. */
async function verComoEstudiante(estudiante: Page, numero: string) {
  await estudiante.goto("/tramites");
  await estudiante.getByRole("button", { name: new RegExp(numero) }).click();
  return estudiante.getByRole("region", { name: `Trámite ${numero}` });
}

test.describe("Trámites", () => {
  test("el estudiante pide una constancia, el administrador la toma y la aprueba, y el estudiante ve el historial", async ({ browser }) => {
    const { estudiante, admin } = await sesiones(browser);

    const numero = await pedir(estudiante, "Constancia de alumno regular", async (formulario) => {
      await formulario.getByRole("textbox", { name: /^Para presentar ante/ }).fill("ANSES");
    });

    const enBandeja = await abrirEnBandeja(admin, numero);
    await expect(enBandeja).toContainText("ANSES");
    await enBandeja.getByRole("button", { name: "Tomar trámite" }).click();
    await expect(admin.getByRole("status")).toContainText(`${numero} quedó en proceso`);
    // La constancia de alumno regular emite PDF: Secretaría puede corregir los datos del titular antes de aprobar.
    const datos = enBandeja.getByRole("form", { name: "Datos oficiales del titular" });
    await datos.getByLabel("Nombre").fill("Ana");
    await datos.getByLabel("Apellido").fill("Pérez");
    await datos.getByLabel("DNI").fill(dniUnico());
    await datos.getByRole("button", { name: "Guardar datos oficiales" }).click();
    await expect(admin.getByRole("status")).toContainText("Datos oficiales guardados");
    await enBandeja.getByRole("textbox", { name: /^Indicación para el estudiante/ }).fill("Retirala en Secretaría de 18 a 21 h");
    await enBandeja.getByRole("button", { name: "Aprobar" }).click();
    await expect(admin.getByRole("status")).toContainText(`${numero} quedó listo`);
    await expect(enBandeja).toContainText("admin@astrea.test");

    const visto = await verComoEstudiante(estudiante, numero);
    await expect(visto).toContainText("Listo");
    await expect(visto).toContainText("Indicación de la Secretaría: Retirala en Secretaría de 18 a 21 h");
    const historial = visto.getByRole("list", { name: "Historial" }).getByRole("listitem");
    await expect(historial).toHaveCount(3);
    await expect(historial.nth(0)).toContainText("Vos");
    await expect(historial.nth(1)).toContainText("Secretaría");
    await expect(visto).not.toContainText("admin@astrea.test");

    // En el inicio le aparece el aviso (sin número de trámite) hasta que toca "Entendido".
    await estudiante.goto("/inicio");
    const avisos = estudiante.getByRole("list", { name: "Avisos de tus trámites" });
    await expect(avisos).toContainText("Tu constancia de alumno regular está lista para retirar.");
    await expect(avisos).not.toContainText(numero);
    await avisos.getByRole("button", { name: "Entendido" }).click();
    await expect(avisos).toBeHidden();
    await estudiante.reload();
    await expect(estudiante.getByRole("heading", { level: 1 })).toBeVisible();
    await expect(estudiante.getByRole("list", { name: "Avisos de tus trámites" })).toBeHidden();
  });

  test("un trámite rechazado muestra el motivo al estudiante", async ({ browser }) => {
    const { estudiante, admin } = await sesiones(browser);

    const numero = await pedir(estudiante, "Constancia de examen", async (formulario) => {
      await formulario.getByRole("textbox", { name: /^Materia/ }).fill("Programación II");
      await formulario.getByLabel("Fecha del examen").fill("2026-12-10");
    });

    const enBandeja = await abrirEnBandeja(admin, numero);
    await enBandeja.getByRole("textbox", { name: /^Motivo del rechazo/ }).fill("No figurás en el acta de esa mesa");
    await enBandeja.getByRole("button", { name: "Rechazar" }).click();
    await expect(admin.getByRole("status")).toContainText(`${numero} fue rechazado`);

    const visto = await verComoEstudiante(estudiante, numero);
    await expect(visto).toContainText("Rechazado");
    await expect(visto).toContainText("Motivo del rechazo: No figurás en el acta de esa mesa");
  });
});
