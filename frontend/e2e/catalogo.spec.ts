import { expect, test } from "@playwright/test";
import { ingresarComoAdministrador, ingresarComoEstudianteNuevo } from "./sesiones";

test.describe("Catálogo académico", () => {
  test("un estudiante consulta el plan de estudios de su carrera", async ({ page }) => {
    await ingresarComoEstudianteNuevo(page);

    await page.getByRole("link", { name: "Plan de estudios" }).first().click();
    await expect(page).toHaveURL(/\/plan-de-estudios$/);
    await page.getByLabel("Carrera").selectOption({ label: "Tecnicatura Superior en Desarrollo de Software" });

    await expect(page.getByTestId("plan-vigente")).toContainText("Res. 2120/16");
    const segundoAnio = page.getByRole("region", { name: "Segundo año" });
    await expect(segundoAnio.getByText("Programación I", { exact: true })).toBeVisible();
    await expect(segundoAnio.getByText("Práctica Profesionalizante I", { exact: true })).toBeVisible();
    await expect(page.getByRole("region", { name: "Tercer año" }).getByText("Programación II", { exact: true })).toBeVisible();
    await expect(page.getByRole("region", { name: "Cuarto año" })).toHaveCount(0);
  });

  test("el administrador carga una carrera nueva y el estudiante la ve", async ({ page, browser }) => {
    const codigo = `E2E${Date.now().toString(36)}${Math.random().toString(36).slice(2, 5)}`.toUpperCase();
    const nombre = `Tecnicatura de Prueba ${codigo}`;

    await ingresarComoAdministrador(page);
    await page.getByRole("navigation", { name: "Secciones de administración" }).getByRole("link", { name: /Plan de estudios/ }).click();
    await expect(page).toHaveURL(/\/admin\/academico$/);

    const nuevaCarrera = page.getByRole("form", { name: "Nueva carrera" });
    await nuevaCarrera.getByLabel("Código").fill(codigo);
    await nuevaCarrera.getByLabel("Nombre").fill(nombre);
    await nuevaCarrera.getByLabel("Duración (años)").fill("2");
    await nuevaCarrera.getByRole("button", { name: "Guardar" }).click();
    await expect(page.getByRole("heading", { level: 2, name: nombre })).toBeVisible();

    await page.getByRole("button", { name: "Nuevo plan" }).click();
    const nuevoPlan = page.getByRole("form", { name: "Nuevo plan" });
    await nuevoPlan.getByLabel("Resolución").fill("Res. 99/26");
    await nuevoPlan.getByRole("button", { name: "Guardar" }).click();

    const agregar = page.getByRole("form", { name: "Agregar materia" });
    await agregar.getByLabel("Materia").fill("Testing Automatizado");
    await agregar.getByLabel("Año").selectOption("2");
    await agregar.getByLabel("Cursado").selectOption("Segundo");
    await agregar.getByRole("button", { name: "Guardar" }).click();
    await expect(page.getByRole("region", { name: "Segundo año" }).getByText("Testing Automatizado")).toBeVisible();

    // Otro navegador, otra persona: un estudiante nuevo ve la carrera recién cargada.
    const contexto = await browser.newContext();
    const estudiante = await contexto.newPage();
    await ingresarComoEstudianteNuevo(estudiante);
    await estudiante.goto("/plan-de-estudios");
    await estudiante.getByLabel("Carrera").selectOption({ label: nombre });
    const segundoAnio = estudiante.getByRole("region", { name: "Segundo año" });
    await expect(segundoAnio.getByText("Testing Automatizado")).toBeVisible();
    await expect(segundoAnio.getByText("2.º cuatrimestre")).toBeVisible();
    await contexto.close();
  });

  test("Secretaría desactiva una carrera, sale de la lista y la restaura", async ({ page }) => {
    await ingresarComoAdministrador(page);
    await page.goto("/admin/academico");
    const marca = `${Date.now()}`.slice(-6);
    const nombre = `Carrera de prueba ${marca}`;
    const nueva = page.getByRole("form", { name: "Nueva carrera" });
    await nueva.getByLabel("Código").fill(`CP${marca}`);
    await nueva.getByLabel("Nombre").fill(nombre);
    await nueva.getByRole("button", { name: "Guardar" }).click();
    const activas = page.getByRole("list", { name: "Carreras activas" });
    await expect(activas).toContainText(nombre);
    await expect(page.getByRole("heading", { name: nombre, level: 2 })).toBeVisible();

    await page.getByRole("button", { name: "Desactivar carrera" }).click();
    await expect(page.getByRole("status")).toContainText(`Se desactivó ${nombre}`);
    await expect(activas).not.toContainText(nombre);
    await expect(page.getByRole("heading", { name: nombre, level: 2 })).toHaveCount(0);

    await page.getByRole("button", { name: /^Restaurar carreras desactivadas \(\d+\)$/ }).click();
    await page.getByRole("list", { name: "Carreras desactivadas" }).getByRole("listitem", { name: nombre }).getByRole("button", { name: "Restaurar" }).click();
    await expect(page.getByRole("status")).toContainText(`Se restauró ${nombre}`);
    await expect(activas).toContainText(nombre);
    await expect(page.getByRole("heading", { name: nombre, level: 2 })).toBeVisible();

    // Queda guardado.
    await page.reload();
    await expect(page.getByRole("list", { name: "Carreras activas" })).toContainText(nombre);
  });

  test("un estudiante no puede entrar a la administración del plan de estudios", async ({ page }) => {
    await ingresarComoEstudianteNuevo(page);

    await page.goto("/admin/academico");

    await expect(page.getByRole("heading", { name: "No tenés acceso a esta sección" })).toBeVisible();
    await expect(page.getByRole("form", { name: "Nueva carrera" })).toHaveCount(0);
  });
});
