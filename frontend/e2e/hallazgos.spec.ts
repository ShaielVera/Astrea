import { expect, test, type Page } from "@playwright/test";
import { ingresarComoAdministrador, ingresarComoEstudianteNuevo } from "./sesiones";

/** PNG de 1x1 píxel. */
const PNG = Buffer.from("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNkYPhfDwAChwGA60e6kgAAAABJRU5ErkJggg==", "base64");

async function buscar(page: Page, texto: string) {
  await page.goto("/hallazgos");
  const filtros = page.getByRole("form", { name: "Buscar hallazgos" });
  await filtros.getByRole("searchbox", { name: "Qué buscás" }).fill(texto);
  await filtros.getByRole("button", { name: "Buscar" }).click();
}

test.describe("Hallazgos", () => {
  test("se publica con foto, otro estudiante lo encuentra sin ver lo privado, la Secretaría lo entrega y desaparece", async ({ browser }) => {
    const titulo = `Termo ${Math.random().toString(36).slice(2, 8)}`;
    const quienEncontro = await (await browser.newContext()).newPage();
    const duenio = await (await browser.newContext()).newPage();
    const admin = await (await browser.newContext()).newPage();
    await ingresarComoEstudianteNuevo(quienEncontro);
    await ingresarComoEstudianteNuevo(duenio);
    await ingresarComoAdministrador(admin);

    // 1. Quien lo encontró lo publica, con foto y detalles privados.
    await quienEncontro.getByRole("navigation", { name: "Principal" }).getByRole("link", { name: "Hallazgos" }).click();
    await expect(quienEncontro).toHaveURL(/\/hallazgos$/);
    await quienEncontro.getByRole("button", { name: "Encontré algo" }).click();
    const formulario = quienEncontro.getByRole("form", { name: "Publicar un hallazgo" });
    await formulario.getByLabel("Qué encontraste").fill(titulo);
    await formulario.getByRole("combobox", { name: /^Categoría/ }).selectOption("TermoOBotella");
    await formulario.getByLabel("Dónde lo encontraste").fill("Aula 3, planta baja");
    await formulario.getByRole("combobox", { name: /^Dónde lo dejás/ }).selectOption({ label: "Aula de Infraestructura" });
    await formulario.getByLabel(/^Foto/).setInputFiles({ name: "termo.png", mimeType: "image/png", buffer: PNG });
    await formulario.getByLabel(/^Detalles privados/).fill("Tiene una calcomanía de Racing");
    await formulario.getByRole("button", { name: "Publicar" }).click();
    await expect(quienEncontro.getByText(`Publicaste "${titulo}"`)).toBeVisible();
    await expect(quienEncontro.getByRole("region", { name: "Lo que publicaste" })).toContainText("Tiene una calcomanía de Racing");

    // 2. El dueño lo busca: ve dónde retirarlo y la foto, pero no los detalles privados.
    await buscar(duenio, titulo);
    const tarjeta = duenio.getByRole("article", { name: titulo });
    await expect(tarjeta).toContainText("Aula de Infraestructura");
    await expect(tarjeta).toContainText("Aula 3, planta baja");
    await expect(tarjeta.getByRole("img", { name: `Foto de ${titulo}` })).toBeVisible();
    await tarjeta.getByRole("button", { name: "Ver en grande" }).click();
    const ampliada = duenio.getByRole("dialog", { name: `Foto de ${titulo}` });
    await expect(ampliada.getByRole("img")).toBeVisible();
    await ampliada.getByRole("button", { name: "Acercar" }).click();
    await expect(ampliada).toContainText("150 %");
    await ampliada.getByRole("button", { name: "Alejar" }).click();
    await expect(ampliada).toContainText("100 %");
    await ampliada.getByRole("button", { name: "Cerrar" }).click();
    await expect(ampliada).toBeHidden();
    await expect(duenio.getByRole("main")).not.toContainText("Racing");
    await expect(duenio.getByRole("note", { name: "Cómo retirar un objeto" })).toContainText("ticket de compra");

    // 3. La Secretaría verifica en persona y lo entrega.
    await admin.getByRole("navigation", { name: "Secciones de administración" }).getByRole("link", { name: /Hallazgos/ }).click();
    await expect(admin).toHaveURL(/\/admin\/hallazgos$/);
    const enAdmin = admin.getByRole("article", { name: titulo });
    await expect(enAdmin).toContainText("Tiene una calcomanía de Racing");
    await enAdmin.getByRole("button", { name: "Entregar" }).click();
    const entrega = enAdmin.getByRole("form", { name: `Entregar ${titulo}` });
    await entrega.getByLabel("Nombre y apellido de quien lo retira").fill("Juan López");
    await entrega.getByLabel("DNI de quien lo retira").fill("30.123.456");
    await entrega.getByRole("checkbox", { name: /Verifiqué en persona/ }).check();
    await entrega.getByRole("button", { name: "Confirmar entrega" }).click();
    await expect(admin.getByRole("status")).toContainText(`"${titulo}" quedó entregado a Juan López`);

    // 4. Ya no aparece en la búsqueda; quien lo publicó lo ve como entregado.
    await buscar(duenio, titulo);
    await expect(duenio.getByRole("status")).toContainText("No hay objetos publicados");
    await quienEncontro.reload();
    await expect(quienEncontro.getByRole("region", { name: "Lo que publicaste" }).getByRole("article", { name: titulo })).toContainText("Entregado");
  });
});
