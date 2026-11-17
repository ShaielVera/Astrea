import { readFile } from "node:fs/promises";
import { expect, test, type Page } from "@playwright/test";
import { crearCuenta, obtenerIdToken } from "./emulador";
import { E2E, emailUnico, dniUnico } from "./entorno";
import { ingresarComoAdministrador, ingresarComoEstudianteNuevo } from "./sesiones";

/** El estudiante pide una constancia de alumno regular y devuelve el número y el id del trámite. */
async function pedirConstancia(estudiante: Page): Promise<string> {
  await estudiante.goto("/tramites");
  await estudiante.getByRole("combobox", { name: /^Tipo de trámite/ }).selectOption({ label: "Constancia de alumno regular" });
  const formulario = estudiante.getByRole("form", { name: "Constancia de alumno regular" });
  await formulario.getByRole("combobox", { name: /^Carrera/ }).selectOption({ label: "Tecnicatura Superior en Desarrollo de Software" });
  await formulario.getByRole("textbox", { name: /^Para presentar ante/ }).fill("ANSES");
  await formulario.getByRole("button", { name: "Enviar solicitud" }).click();
  const aviso = estudiante.getByRole("status");
  await expect(aviso).toContainText("Tu número de trámite es");
  return (await aviso.textContent())!.match(/T-\d{6}/)![0];
}

test.describe("Documentos oficiales", () => {
  test("aprobar emite el PDF, el titular lo descarga y cualquiera valida el QR; nadie más lo descarga", async ({ browser }) => {
    const estudiante = await (await browser.newContext()).newPage();
    const admin = await (await browser.newContext()).newPage();
    await ingresarComoEstudianteNuevo(estudiante);
    await ingresarComoAdministrador(admin);

    const numero = await pedirConstancia(estudiante);

    // 1. Sin datos oficiales, la aprobación se frena con un mensaje claro.
    await admin.goto("/admin/tramites");
    await admin.getByRole("searchbox", { name: /^Número, email o apellido/ }).fill(numero);
    await admin.getByRole("form", { name: "Filtrar trámites" }).getByRole("button", { name: "Buscar" }).click();
    const [abierto] = await Promise.all([
      admin.waitForResponse((respuesta) => /\/api\/v1\/admin\/tramites\/[0-9a-f-]{36}$/.test(respuesta.url())),
      admin.getByRole("button", { name: new RegExp(numero) }).click(),
    ]);
    const tramiteId = ((await abierto.json()) as { id: string }).id;
    const enBandeja = admin.getByRole("region", { name: `Trámite ${numero}` });
    await enBandeja.getByRole("button", { name: "Aprobar" }).click();
    await expect(admin.getByRole("main").getByRole("alert")).toContainText("datos oficiales");

    // 2. Secretaría carga los datos oficiales y aprueba: se emite el PDF.
    const datos = enBandeja.getByRole("form", { name: "Datos oficiales del titular" });
    await datos.getByLabel("Nombre").fill("Ana");
    await datos.getByLabel("Apellido").fill("Pérez");
    await datos.getByLabel("DNI").fill(dniUnico());
    await datos.getByRole("button", { name: "Guardar datos oficiales" }).click();
    await expect(admin.getByRole("status")).toContainText("Datos oficiales guardados");
    await enBandeja.getByRole("button", { name: "Aprobar" }).click();
    await expect(admin.getByRole("status")).toContainText(`${numero} quedó listo y se emitió el PDF`);
    await expect(enBandeja.getByRole("region", { name: "Documento emitido" })).toContainText("Vigente");

    // 3. El titular lo descarga.
    await estudiante.goto("/tramites");
    await estudiante.getByRole("button", { name: new RegExp(numero) }).click();
    const documento = estudiante.getByRole("region", { name: "Documento emitido" });
    await expect(documento).toContainText("Vigente");
    const [descarga] = await Promise.all([estudiante.waitForEvent("download"), documento.getByRole("button", { name: "Descargar PDF" }).click()]);
    expect(descarga.suggestedFilename()).toMatch(new RegExp(`^${numero}-.*\\.pdf$`));
    const pdf = await readFile((await descarga.path())!);
    expect(pdf.subarray(0, 5).toString("ascii")).toBe("%PDF-");

    // 4. Cualquiera (sin sesión) valida el código del QR y ve solo datos mínimos.
    const codigo = (await documento.getByRole("link").textContent())!.trim();
    const publico = await (await browser.newContext()).newPage();
    await publico.goto(`/validar/${codigo}`);
    const resultado = publico.getByRole("region", { name: "Resultado de la validación" });
    await expect(resultado).toContainText("Documento auténtico y vigente");
    await expect(resultado).toContainText("Constancia de alumno regular");
    await expect(resultado).toContainText("**.***.456");
    await expect(resultado).toContainText(numero);
    await expect(resultado).not.toContainText("Pérez");

    // 5. Otro estudiante no puede descargarlo (la API responde como si no existiera), y sin sesión tampoco.
    const otroEmail = emailUnico("ajeno");
    await crearCuenta(otroEmail, E2E.passwordEstudiantes, false);
    const tokenAjeno = await obtenerIdToken(otroEmail, E2E.passwordEstudiantes);
    const comoAjeno = (ruta: string, method = "GET") =>
      fetch(`${E2E.apiUrl}/api/v1${ruta}`, { method, headers: { Authorization: `Bearer ${tokenAjeno}` } });
    expect((await comoAjeno("/auth/sesion", "POST")).ok).toBe(true);
    expect((await comoAjeno(`/tramites/${tramiteId}/documento`)).status).toBe(404);
    expect((await fetch(`${E2E.apiUrl}/api/v1/tramites/${tramiteId}/documento`)).status).toBe(401);
  });

  test("un código inexistente no valida", async ({ page }) => {
    await page.goto("/validar");
    await page.getByLabel("Código de validación").fill("ZZZZ-ZZZZ-ZZZZ");
    await page.getByRole("button", { name: "Validar" }).click();

    await expect(page).toHaveURL(/\/validar\/ZZZZ-ZZZZ-ZZZZ$/);
    await expect(page.getByRole("region", { name: "Resultado de la validación" })).toContainText("No es un documento de Astrea");
  });
});
