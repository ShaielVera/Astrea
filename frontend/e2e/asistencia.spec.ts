import { expect, test, type Page } from "@playwright/test";
import { completarPerfil, ingresarComoAdministrador, ingresarComoEstudianteNuevo } from "./sesiones";

const CARRERA = "Tecnicatura Superior en Desarrollo de Software";
const MATERIA = "Programación II";

async function elegirMateria(page: Page, formulario: string) {
  const form = page.getByRole("form", { name: formulario });
  await form.getByRole("combobox", { name: /^Carrera/ }).selectOption({ label: CARRERA });
  await form.getByRole("combobox", { name: /^Materia/ }).selectOption({ label: MATERIA });
  return form;
}

test.describe("Confirmación de asistencia", () => {
  test("el profesor confirma quién asiste, el estudiante lo ve y la dirección lo encuentra en el informe", async ({ page, browser }) => {
    const marca = `${Date.now()}`.slice(-6);

    // 1. El estudiante carga su carrera en el perfil (solo se anota en materias de sus carreras) y anota la materia que cursa.
    const estudiante = await ingresarComoEstudianteNuevo(page);
    const apellido = `Prueba${marca}`;
    await completarPerfil(page, { nombre: "Ana", apellido });
    const nombreVisible = `${apellido}, Ana`;
    await page.goto("/mi-cursada");
    const anotarme = await elegirMateria(page, "Anotarme en una materia");
    await anotarme.getByRole("button", { name: "Anotarme" }).click();
    await expect(page.getByRole("status")).toHaveText(`Te anotaste en ${MATERIA}.`);
    await expect(page.getByRole("list", { name: "Materias que curso" })).toContainText(MATERIA);

    // 2. Secretaría le da el rol de docente a un profesor y le asigna la materia.
    const contextoProfe = await browser.newContext();
    const profe = await contextoProfe.newPage();
    const emailProfe = await ingresarComoEstudianteNuevo(profe);

    const contextoAdmin = await browser.newContext();
    const admin = await contextoAdmin.newPage();
    await ingresarComoAdministrador(admin);
    await admin.goto("/admin/usuarios");
    await admin.getByLabel("Buscar por apellido, nombre o DNI").fill(emailProfe);
    const docente = admin.getByRole("list", { name: "Cuentas" }).getByRole("listitem", { name: emailProfe, exact: true });
    await docente.getByRole("checkbox", { name: "Docente" }).check();
    await expect(admin.getByRole("status")).toContainText("ahora es docente");
    const asignar = await elegirMateria(admin, `Asignar materia a ${emailProfe}`);
    await asignar.getByRole("button", { name: "Asignar" }).click();
    await expect(docente.getByRole("list", { name: `Materias de ${emailProfe}` })).toContainText(MATERIA);

    // 3. La dirección abre el pedido de confirmación.
    await admin.goto("/admin/asistencia");
    await admin.getByLabel("Título").fill(`Receso ${marca}`);
    await admin.getByRole("button", { name: "Abrir" }).click();
    await expect(admin.getByRole("status")).toContainText(`Se abrió "Receso ${marca}"`);

    // 4. El profesor (al volver a entrar ya tiene el rol) confirma que el estudiante no asiste.
    await profe.goto("/docente");
    await expect(profe.getByRole("heading", { name: "Confirmar asistencia", level: 1 })).toBeVisible();
    const pedido = profe.getByLabel("Pedido de la dirección");
    const opcion = pedido.locator("option", { hasText: `Receso ${marca}` });
    await pedido.selectOption(await opcion.getAttribute("value"));
    const materia = profe.getByRole("region", { name: new RegExp(`^${MATERIA} \\(`) });
    await materia.getByRole("group", { name: `Asistencia de ${nombreVisible}` }).getByLabel("No asiste").check();
    await materia.getByRole("textbox", { name: `Observación sobre ${nombreVisible}` }).fill("No vino desde abril");
    await materia.getByRole("button", { name: "Guardar" }).click();
    await expect(materia.getByRole("status")).toContainText("Guardado: 1 estudiante");

    // 5. El estudiante ve lo que confirmó su profesor y ya no puede borrarlo dándose de baja.
    await page.reload();
    const confirmaciones = page.getByRole("list", { name: "Confirmaciones de asistencia" });
    await expect(confirmaciones).toContainText("No asiste");
    await expect(confirmaciones).toContainText("No vino desde abril");
    await page.getByRole("button", { name: `Darme de baja de ${MATERIA}` }).click();
    await expect(page.getByRole("alert").filter({ hasText: "hablá con Secretaría" })).toBeVisible();

    // 6. La dirección lo encuentra en el informe.
    const relevamiento = admin.getByRole("combobox", { name: "Relevamiento", exact: true });
    await relevamiento.selectOption(await relevamiento.locator("option", { hasText: `Receso ${marca}` }).getAttribute("value"));
    await admin.getByLabel("Buscar por nombre, email o DNI (opcional)").fill(estudiante);
    await admin.getByRole("button", { name: "Ver informe" }).click();
    const fila = admin.getByRole("list", { name: "Estudiantes del informe" }).getByRole("listitem", { name: nombreVisible });
    await expect(fila).toContainText("1 no asiste");
    await expect(fila).toContainText("No vino desde abril");

    await contextoProfe.close();
    await contextoAdmin.close();
  });

  test("un estudiante no entra al área docente ni a la de asistencia", async ({ page }) => {
    await ingresarComoEstudianteNuevo(page);
    for (const ruta of ["/docente", "/admin/usuarios", "/admin/asistencia"]) {
      await page.goto(ruta);
      await expect(page.getByRole("heading", { name: "No tenés acceso a esta sección" }), ruta).toBeVisible();
    }
  });
});
