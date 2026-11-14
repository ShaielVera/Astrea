import { expect, type Page } from "@playwright/test";
import { E2E, emailUnico } from "./entorno";

/** Crea una cuenta de estudiante nueva desde la interfaz y espera a que llegue a su inicio. */
export async function ingresarComoEstudianteNuevo(page: Page): Promise<string> {
  const email = emailUnico("estudiante");
  await page.goto("/login");
  await page.getByRole("button", { name: "¿No tenés cuenta? Creá una" }).click();
  await page.getByLabel("Email").fill(email);
  await page.getByLabel("Contraseña").fill(E2E.passwordEstudiantes);
  await page.getByRole("button", { name: "Crear cuenta" }).click();
  await expect(page).toHaveURL(/\/inicio$/);
  return email;
}

export async function ingresarComoAdministrador(page: Page): Promise<void> {
  await page.goto("/login");
  await page.getByLabel("Email").fill(E2E.admin.email);
  await page.getByLabel("Contraseña").fill(E2E.admin.password);
  await page.getByRole("button", { name: "Ingresar", exact: true }).click();
  await expect(page).toHaveURL(/\/admin$/);
}
