import { expect, type Page } from "@playwright/test";
import { E2E, dniUnico, emailUnico } from "./entorno";

/** Crea una cuenta de estudiante nueva desde la interfaz y espera a que llegue a su inicio. */
export async function ingresarComoEstudianteNuevo(page: Page): Promise<string> {
  const email = emailUnico("estudiante");
  await page.goto("/login");
  await page.getByRole("button", { name: "Crear mi cuenta" }).click();
  await page.getByLabel("Email").fill(email);
  await page.getByLabel("Contraseña", { exact: true }).fill(E2E.passwordEstudiantes);
  await page.getByRole("button", { name: "Crear cuenta" }).click();
  await expect(page).toHaveURL(/\/inicio$/);
  return email;
}

export async function ingresarComoAdministrador(page: Page): Promise<void> {
  await page.goto("/login");
  await page.getByLabel("Email").fill(E2E.admin.email);
  await page.getByLabel("Contraseña", { exact: true }).fill(E2E.admin.password);
  await page.getByRole("button", { name: "Ingresar", exact: true }).click();
  await expect(page).toHaveURL(/\/admin$/);
}

/**
 * El estudiante completa "Mi perfil" (nombre, apellido, DNI, localidad y carrera). Los trámites solo dejan elegir
 * las carreras del perfil, así que hace falta antes de pedir uno.
 */
export async function completarPerfil(page: Page, datos: { nombre?: string; apellido?: string } = {}): Promise<void> {
  await page.goto("/mi-perfil");
  const formulario = page.getByRole("form", { name: "Datos personales" });
  await formulario.getByLabel("Nombre", { exact: true }).fill(datos.nombre ?? "Ana");
  await formulario.getByLabel("Apellido", { exact: true }).fill(datos.apellido ?? "Pérez");
  await formulario.getByLabel("DNI", { exact: true }).fill(dniUnico());
  await formulario.getByRole("combobox", { name: "Localidad" }).fill("san jus");
  await page.getByRole("option", { name: /^San Justo/ }).click();
  await formulario.getByRole("combobox", { name: "Carrera 1" }).selectOption({ label: "Tecnicatura Superior en Desarrollo de Software" });
  await formulario.getByRole("button", { name: "Guardar datos" }).click();
  await expect(formulario.getByText("Guardado.")).toBeVisible();
}
