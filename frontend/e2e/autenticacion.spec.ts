import { expect, test, type Page } from "@playwright/test";
import { E2E, emailUnico } from "./entorno";

async function completarFormulario(page: Page, email: string, password: string) {
  await page.getByLabel("Email").fill(email);
  await page.getByLabel("Contraseña").fill(password);
}

async function ingresar(page: Page, email: string, password: string) {
  await page.goto("/login");
  await completarFormulario(page, email, password);
  await page.getByRole("button", { name: "Ingresar", exact: true }).click();
}

test.describe("Identidad y acceso", () => {
  test("un estudiante crea su cuenta, ve su área y cierra sesión", async ({ page }) => {
    const email = emailUnico("estudiante");

    await page.goto("/login");
    await page.getByRole("button", { name: "¿No tenés cuenta? Creá una" }).click();
    await completarFormulario(page, email, E2E.passwordEstudiantes);
    await page.getByRole("button", { name: "Crear cuenta" }).click();

    await expect(page).toHaveURL(/\/inicio$/);
    await expect(page.getByRole("heading", { level: 1, name: "Mi inicio" })).toBeVisible();
    await expect(page.getByTestId("email-usuario")).toHaveText(email);
    await expect(page.getByRole("link", { name: "Administración" })).toHaveCount(0);

    await page.getByRole("button", { name: "Cerrar sesión" }).click();
    await expect(page).toHaveURL(/\/login$/);

    await page.goto("/inicio");
    await expect(page).toHaveURL(/\/login$/);
  });

  test("un estudiante no puede entrar al área de administración", async ({ page }) => {
    const email = emailUnico("curioso");
    await page.goto("/login");
    await page.getByRole("button", { name: "¿No tenés cuenta? Creá una" }).click();
    await completarFormulario(page, email, E2E.passwordEstudiantes);
    await page.getByRole("button", { name: "Crear cuenta" }).click();
    await expect(page).toHaveURL(/\/inicio$/);

    await page.goto("/admin");

    await expect(page.getByRole("heading", { name: "No tenés acceso a esta sección" })).toBeVisible();
    await expect(page.getByTestId("total-estudiantes")).toHaveCount(0);
  });

  test("el administrador ingresa a su panel y ve el resumen de usuarios", async ({ page }) => {
    await ingresar(page, E2E.admin.email, E2E.admin.password);

    await expect(page).toHaveURL(/\/admin$/);
    await expect(page.getByRole("heading", { level: 1, name: "Administración" })).toBeVisible();
    await expect(page.getByTestId("total-administradores")).not.toHaveText("0");

    await page.getByRole("link", { name: "Mi inicio" }).click();
    await expect(page.getByRole("heading", { level: 1, name: "Mi inicio" })).toBeVisible();
  });

  test("con una contraseña incorrecta muestra un error y no ingresa", async ({ page }) => {
    await ingresar(page, E2E.admin.email, "contraseña-equivocada");

    // Se busca dentro de <main>: Next agrega otro role="alert" oculto para anunciar navegaciones.
    await expect(page.getByRole("main").getByRole("alert")).toHaveText("El email o la contraseña no son correctos.");
    await expect(page).toHaveURL(/\/login$/);
  });

  test("sin sesión, el área de administración redirige al login", async ({ page }) => {
    await page.goto("/admin");

    await expect(page).toHaveURL(/\/login$/);
    await expect(page.getByRole("heading", { name: "Ingresar a Astrea" })).toBeVisible();
  });
});
