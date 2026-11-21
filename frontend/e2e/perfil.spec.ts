import { expect, test } from "@playwright/test";
import { dniUnico } from "./entorno";
import { completarPerfil, ingresarComoEstudianteNuevo } from "./sesiones";

/** PNG de 1x1 píxel. */
const PNG = Buffer.from("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNkYPhfDwAChwGA60e6kgAAAABJRU5ErkJggg==", "base64");

test.describe("Mi perfil y arrepentimientos", () => {
  test("el estudiante completa sus datos (con localidad y carreras) y su contacto, y pide el código de validación", async ({ page }) => {
    await ingresarComoEstudianteNuevo(page);
    await expect(page.getByRole("status").filter({ hasText: "Completá tu perfil" })).toBeVisible();

    await page.getByRole("link", { name: "Mi perfil" }).first().click();
    await expect(page.getByRole("heading", { name: "Mi perfil", level: 1 })).toBeVisible();

    const datos = page.getByRole("form", { name: "Datos personales" });
    await datos.getByLabel("Nombre", { exact: true }).fill("Brenda");
    await datos.getByLabel("Apellido", { exact: true }).fill("Vera");
    await datos.getByLabel("DNI", { exact: true }).fill(dniUnico());

    // La localidad se elige de la lista: escribir filtra (sin importar tildes, y también por otros nombres: "Villa Soledad" es Soledad).
    const localidad = datos.getByRole("combobox", { name: "Localidad" });
    await localidad.fill("villa sol");
    const opciones = page.getByRole("listbox", { name: "Localidad" }).getByRole("option");
    await expect(opciones).toHaveCount(1);
    await opciones.filter({ hasText: "Soledad" }).click();
    await expect(localidad).toHaveValue("Soledad");

    // Una carrera y después otra más.
    await datos.getByRole("combobox", { name: "Carrera 1" }).selectOption({ label: "Tecnicatura Superior en Desarrollo de Software" });
    await datos.getByRole("button", { name: "+ Agregar otra carrera" }).click();
    await datos.getByRole("combobox", { name: "Carrera 2" }).selectOption({ index: 1 });
    await datos.getByRole("button", { name: "Guardar datos" }).click();
    await expect(datos.getByText("Guardado.")).toBeVisible();
    await expect(page.getByRole("region", { name: "Datos personales" })).toContainText("Completos");

    const contacto = page.getByRole("form", { name: "Contacto" });
    await contacto.getByLabel("Email de contacto").fill(`brenda-${Date.now()}@gmail.com`);
    await contacto.getByLabel(/^Celular/).fill("0342 15 412-3456");
    await contacto.getByRole("button", { name: "Guardar contacto" }).click();

    const celular = page.getByRole("group", { name: "Validación del celular" });
    await expect(celular).toContainText("342 412-3456");
    await expect(celular).toContainText("Sin validar");
    const email = page.getByRole("group", { name: "Validación del email" });
    await email.getByRole("button", { name: "Enviar código" }).click();
    // En E2E no hay SMTP: el código queda en el registro del servidor (modo demostración).
    await expect(email).toContainText("Modo demostración");
    await expect(email.getByLabel("Código de 6 dígitos")).toBeVisible();
  });

  test("un trámite se puede descartar antes de enviarlo y cancelar después", async ({ page }) => {
    await ingresarComoEstudianteNuevo(page);
    await completarPerfil(page);
    await page.goto("/tramites");
    const tipo = page.getByRole("combobox", { name: /^Tipo de trámite/ });
    await tipo.selectOption({ label: "Constancia de alumno regular" });
    const formulario = page.getByRole("form", { name: "Constancia de alumno regular" });
    await formulario.getByRole("button", { name: "Cancelar" }).click();
    await expect(formulario).toBeHidden();
    await expect(tipo).toHaveValue("");

    await tipo.selectOption({ label: "Constancia de alumno regular" });
    // Solo aparece la carrera del perfil, y como es una sola ya viene elegida.
    const carrera = formulario.getByRole("combobox", { name: /^Carrera/ });
    await expect(carrera.getByRole("option")).toHaveCount(2);
    await expect(carrera).not.toHaveValue("");
    await formulario.getByLabel(/^Para presentar ante/).fill("ANSES");
    await formulario.getByRole("button", { name: "Enviar solicitud" }).click();
    const numero = (await page.getByRole("status").filter({ hasText: "pediste" }).textContent())!.match(/T-\d{6}/)![0];

    const detalle = page.getByRole("region", { name: `Trámite ${numero}` });
    await detalle.getByRole("button", { name: "Cancelar trámite" }).click();
    await detalle.getByRole("button", { name: "Sí, cancelar" }).click();
    await expect(page.getByRole("status").filter({ hasText: `Cancelaste el trámite ${numero}` })).toBeVisible();
    await expect(detalle.getByRole("list", { name: "Historial" }).getByRole("listitem")).toHaveCount(2);
    await expect(detalle.getByRole("button", { name: "Cancelar trámite" })).toBeHidden();
  });

  test("antes de publicar un hallazgo se puede quitar la foto elegida por error", async ({ page }) => {
    await ingresarComoEstudianteNuevo(page);
    await page.goto("/hallazgos");
    await page.getByRole("button", { name: "Encontré algo" }).click();
    const formulario = page.getByRole("form", { name: "Publicar un hallazgo" });
    const foto = formulario.getByLabel(/^Foto/);

    await foto.setInputFiles({ name: "equivocada.png", mimeType: "image/png", buffer: PNG });
    await expect(formulario.getByRole("img", { name: "Vista previa de la foto elegida" })).toBeVisible();
    await formulario.getByRole("button", { name: "Quitar foto" }).click();

    await expect(formulario.getByRole("img", { name: "Vista previa de la foto elegida" })).toBeHidden();
    await expect(foto).toHaveValue("");
  });
});
