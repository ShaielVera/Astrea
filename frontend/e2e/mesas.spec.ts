import { expect, test, type Page } from "@playwright/test";
import { ingresarComoAdministrador, ingresarComoEstudianteNuevo } from "./sesiones";

/** En pantallas chicas la página no debe desbordar a lo ancho (los nombres de carrera son largos). */
async function sinDesbordeHorizontal(page: Page) {
  const desborde = await page.evaluate(() => document.documentElement.scrollWidth - document.documentElement.clientWidth);
  expect(desborde).toBeLessThanOrEqual(0);
}

/** AAAA-MM-DD dentro de `dias` días (en UTC; alcanza para elegir una fecha futura). */
function enDias(dias: number): string {
  return new Date(Date.now() + dias * 86_400_000).toISOString().slice(0, 10);
}

/** El estudiante busca mesas de Programación II de TSDS por un docente, escrito sin tilde. */
async function buscarComoEstudiante(page: Page, docente: string) {
  await page.goto("/mesas");
  const filtros = page.getByRole("form", { name: "Buscar mesas" });
  await filtros.getByRole("combobox", { name: /^Carrera/ }).selectOption({ label: "Tecnicatura Superior en Desarrollo de Software" });
  await filtros.getByRole("combobox", { name: /^Materia/ }).selectOption({ label: "3.º · Programación II" });
  await filtros.getByRole("searchbox", { name: "Materia o docente" }).fill(docente);
  await sinDesbordeHorizontal(page);
  await filtros.getByRole("button", { name: "Buscar" }).click();
}

test.describe("Mesas de examen", () => {
  test("el administrador publica una mesa, el estudiante la encuentra y ve su cancelación", async ({ browser }) => {
    const sufijo = Math.random().toString(36).slice(2, 7);
    const presidente = `Prof. Núñez ${sufijo}`;
    const fecha = enDias(40);

    const admin = await (await browser.newContext()).newPage();
    const estudiante = await (await browser.newContext()).newPage();
    await ingresarComoAdministrador(admin);
    await ingresarComoEstudianteNuevo(estudiante);

    // 1. El administrador carga la mesa como borrador.
    await admin.getByRole("navigation", { name: "Secciones de administración" }).getByRole("link", { name: /Mesas de examen/ }).click();
    await expect(admin).toHaveURL(/\/admin\/mesas$/);
    const nueva = admin.getByRole("form", { name: "Nueva mesa" });
    await nueva.getByRole("combobox", { name: /^Carrera/ }).selectOption({ label: "Tecnicatura Superior en Desarrollo de Software" });
    await nueva.getByRole("combobox", { name: /^Materia/ }).selectOption({ label: "3.º · Programación II" });
    await nueva.getByLabel("Turno").fill("Diciembre E2E");
    await nueva.getByLabel("Fecha").fill(fecha);
    await nueva.getByLabel("Hora").fill("18:30");
    await nueva.getByLabel("Aula").fill("Laboratorio 2");
    await nueva.getByLabel("Tribunal").fill(`${presidente}\nProf. Ana Gómez`);
    await sinDesbordeHorizontal(admin);
    await nueva.getByRole("button", { name: "Guardar borrador" }).click();
    await expect(admin.getByRole("status")).toContainText("Borrador de Programación II guardado");

    // 2. Mientras es borrador, el estudiante no la ve.
    await buscarComoEstudiante(estudiante, `nunez ${sufijo}`);
    await expect(estudiante.getByRole("status")).toContainText("No hay mesas publicadas");

    // 3. El administrador la publica.
    const mesaAdmin = admin.getByRole("listitem").filter({ hasText: presidente });
    await mesaAdmin.getByRole("button", { name: "Publicar Programación II" }).click();
    await expect(mesaAdmin).toContainText("Publicada");

    // 4. El estudiante la encuentra con fecha, hora, aula y tribunal.
    await buscarComoEstudiante(estudiante, `nunez ${sufijo}`);
    await expect(estudiante.getByRole("status")).toContainText("1 mesa encontrada");
    const mesa = estudiante.getByRole("listitem", { name: "Programación II, 18:30" });
    await expect(mesa).toContainText("Laboratorio 2");
    await expect(mesa).toContainText(`${presidente} · Prof. Ana Gómez`);
    await expect(mesa).toContainText("Diciembre E2E, 1.º llamado");

    // 5. Si se cancela, sigue visible con el motivo.
    await mesaAdmin.getByRole("button", { name: "Cancelar Programación II" }).click();
    await mesaAdmin.getByLabel("Motivo de la cancelación").fill("Licencia del tribunal");
    await mesaAdmin.getByRole("button", { name: "Confirmar cancelación" }).click();
    await expect(mesaAdmin).toContainText("Cancelada");

    await buscarComoEstudiante(estudiante, `nunez ${sufijo}`);
    await expect(estudiante.getByRole("status")).toContainText("1 mesa encontrada");
    await expect(mesa).toContainText("Cancelada");
    await expect(mesa).toContainText("Motivo: Licencia del tribunal");
  });

  test("filtra por rango de fechas y avisa si el rango es inválido", async ({ page }) => {
    await ingresarComoEstudianteNuevo(page);
    await page.getByRole("navigation", { name: "Principal" }).getByRole("link", { name: "Mesas de examen" }).click();
    await expect(page).toHaveURL(/\/mesas$/);

    const filtros = page.getByRole("form", { name: "Buscar mesas" });
    await filtros.getByLabel("Desde").fill(enDias(20));
    await filtros.getByLabel("Hasta").fill(enDias(10));
    await filtros.getByRole("button", { name: "Buscar" }).click();

    // Dentro de <main>: Next agrega otro role="alert" oculto para anunciar navegaciones.
    await expect(page.getByRole("main").getByRole("alert")).toContainText("no puede ser posterior");
  });
});
