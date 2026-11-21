import { expect, test } from "@playwright/test";
import { E2E } from "./entorno";
import { pdfConTexto } from "./pdf";
import { ingresarComoAdministrador, ingresarComoEstudianteNuevo } from "./sesiones";

const CARRERA = "Tecnicatura Superior en Desarrollo de Software";

test.describe("Usuarios y roles", () => {
  test("Secretaría encuentra una cuenta por su email, la hace administradora y no puede quitarse su propio rol", async ({ page, browser }) => {
    const nuevo = await (await browser.newContext()).newPage();
    const email = await ingresarComoEstudianteNuevo(nuevo);

    await ingresarComoAdministrador(page);
    await page.getByRole("navigation", { name: "Secciones de administración" }).getByRole("link", { name: /Usuarios y roles/ }).click();
    await expect(page.getByRole("heading", { name: "Usuarios y roles", level: 1 })).toBeVisible();

    // La cuenta propia está en la lista, con el rol de administrador sin poder quitarlo.
    await page.getByLabel("Buscar por apellido, nombre o DNI").fill(E2E.admin.email);
    const propia = page.getByRole("list", { name: "Cuentas" }).getByRole("listitem", { name: E2E.admin.email, exact: true });
    await expect(propia).toContainText("(vos)");
    await expect(propia.getByRole("checkbox", { name: "Administrador" })).toBeChecked();
    await expect(propia.getByRole("checkbox", { name: "Administrador" })).toBeDisabled();

    // La cuenta nueva acaba de entrar: se ve cuándo y se la hace administradora.
    await page.getByLabel("Buscar por apellido, nombre o DNI").fill(email);
    const cuenta = page.getByRole("list", { name: "Cuentas" }).getByRole("listitem", { name: email, exact: true });
    await expect(cuenta).toContainText(/Último ingreso: (Recién|Hace \d+ minutos?)/);
    await cuenta.getByRole("checkbox", { name: "Administrador" }).check();
    await expect(page.getByRole("status")).toContainText(`${email} ahora es administrador`);

    // Al volver a cargar, ya entra a Administración.
    await nuevo.goto("/admin");
    await expect(nuevo.getByRole("heading", { name: "Administración", level: 1 })).toBeVisible();

    // Quitarle el rol pide confirmación.
    await cuenta.getByRole("checkbox", { name: "Administrador" }).uncheck();
    await cuenta.getByRole("button", { name: "Sí, quitar" }).click();
    await expect(page.getByRole("status")).toContainText(`${email} ya no es administrador`);
    await expect(cuenta.getByRole("checkbox", { name: "Administrador" })).not.toBeChecked();
  });

  test("Secretaría carga los horarios de una carrera y el estudiante los ve en Plan de estudios", async ({ page, browser }) => {
    const marca = Math.random().toString(36).slice(2, 8);
    await ingresarComoAdministrador(page);
    await page.goto("/admin/documentos");
    const formulario = page.getByRole("form", { name: "Cargar documento" });
    await formulario.getByLabel(/^Archivo PDF/).setInputFiles({
      name: "horarios.pdf",
      mimeType: "application/pdf",
      buffer: pdfConTexto(["HORARIOS 2026 - 1 ANO T.S.D.S.", "LUNES 13,10 PROGRAMACION I RAMIREZ R.A.", marca]),
    });
    await formulario.getByLabel("Título").fill(`Horarios 2026 TSDS ${marca}`);
    await formulario.getByLabel("Tipo").selectOption("Horario");
    await formulario.getByRole("combobox", { name: /^Carrera/ }).selectOption({ label: CARRERA });
    await formulario.getByLabel("Fecha del documento (opcional)").fill("2026-03-11");
    await formulario.getByRole("button", { name: "Cargar" }).click();
    await expect(page.getByRole("main").getByText(`Se cargó "Horarios 2026 TSDS ${marca}"`)).toBeVisible();

    const estudiante = await (await browser.newContext()).newPage();
    await ingresarComoEstudianteNuevo(estudiante);
    await estudiante.goto("/plan-de-estudios");
    await estudiante.getByRole("combobox", { name: "Carrera" }).selectOption({ label: CARRERA });
    const horarios = estudiante.getByRole("region", { name: "Horarios 2026" });
    await horarios.getByRole("button", { name: "Ver horarios 2026" }).click();
    await expect(horarios.locator("iframe")).toBeVisible();
    await expect(horarios.getByRole("button", { name: "Ocultar horarios" })).toBeVisible();
  });
});
