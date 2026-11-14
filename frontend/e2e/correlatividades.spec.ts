import { expect, test } from "@playwright/test";
import { ingresarComoAdministrador, ingresarComoEstudianteNuevo } from "./sesiones";

test.describe("Correlatividades", () => {
  test("un estudiante ve los requisitos oficiales, recorre el árbol y verifica su situación", async ({ page }) => {
    await ingresarComoEstudianteNuevo(page);

    await page.getByRole("navigation", { name: "Principal" }).getByRole("link", { name: "Correlatividades" }).click();
    await expect(page).toHaveURL(/\/correlatividades$/);
    await page.getByLabel("Carrera").selectOption({ label: "Tecnicatura Superior en Desarrollo de Software" });
    await page.getByLabel("Año").selectOption({ label: "Tercer año" });
    await page.getByRole("navigation", { name: "Lista de materias" }).getByRole("button", { name: "Programación II" }).click();

    await expect(page.getByRole("heading", { level: 2, name: "Programación II" })).toBeVisible();
    const paraRendir = page.getByRole("region", { name: "Para rendir el final" });
    await expect(paraRendir.getByRole("listitem")).toHaveCount(1);
    await expect(paraRendir.getByRole("listitem")).toContainText("Programación I");
    await expect(paraRendir.getByRole("listitem")).toContainText("Aprobada");
    await expect(page.getByRole("region", { name: "Para cursar" })).toContainText("No tiene correlatividades.");

    // Árbol: desde un requisito se ven sus propias correlatividades, y se puede volver.
    await paraRendir.getByRole("button", { name: "Programación I" }).click();
    await expect(page.getByRole("heading", { level: 2, name: "Programación I" })).toBeVisible();
    await expect(page.getByRole("region", { name: "Para rendir el final" })).toContainText("Lógica y Estructura de Datos");
    await page.getByRole("button", { name: "Volver" }).click();
    await expect(page.getByRole("heading", { level: 2, name: "Programación II" })).toBeVisible();

    // Regularizada no alcanza cuando se pide aprobada.
    const simulacion = page.getByRole("region", { name: "¿Puedo cursarla o rendirla?" });
    await simulacion.getByLabel("Programación I").selectOption("Regularizada");
    await simulacion.getByRole("button", { name: "Verificar para rendir" }).click();
    await expect(simulacion.getByRole("status")).toContainText("Todavía no podés rendirla");
    await expect(simulacion.getByRole("status")).toContainText("Programación I (aprobada)");

    await simulacion.getByLabel("Programación I").selectOption("Aprobada");
    await simulacion.getByRole("button", { name: "Verificar para rendir" }).click();
    await expect(simulacion.getByRole("status")).toContainText("Cumplís las correlatividades para rendirla");
  });

  test("muestra regularizadas y aprobadas por separado, alternativas y requisitos que verifica Bedelía", async ({ page }) => {
    await ingresarComoEstudianteNuevo(page);
    await page.goto("/correlatividades");
    await page.getByLabel("Carrera").selectOption({ label: "Profesorado de Educación Tecnológica" });
    await page.getByLabel("Año").selectOption({ label: "Tercer año" });
    await page.getByRole("button", { name: "Práctica Docente III: La Clase" }).click();

    const paraCursar = page.getByRole("region", { name: "Para cursar" });
    await expect(paraCursar).toContainText("Al menos una de estas:");
    await expect(paraCursar).toContainText("Procesos Productivos");
    await expect(paraCursar).toContainText("Diseño y Producción Tecnológica II");
    await expect(paraCursar).toContainText("Lo verifica Bedelía");
    await expect(paraCursar.getByText("Regularizada").first()).toBeVisible();
    await expect(paraCursar.getByText("Aprobada").first()).toBeVisible();
  });

  test("el administrador agrega y quita una correlatividad", async ({ page }) => {
    const codigo = `E2E${Date.now().toString(36)}${Math.random().toString(36).slice(2, 5)}`.toUpperCase();
    await ingresarComoAdministrador(page);
    await page.goto("/admin/academico");

    const nuevaCarrera = page.getByRole("form", { name: "Nueva carrera" });
    await nuevaCarrera.getByLabel("Código").fill(codigo);
    await nuevaCarrera.getByLabel("Nombre").fill(`Tecnicatura de Prueba ${codigo}`);
    await nuevaCarrera.getByLabel("Duración (años)").fill("2");
    await nuevaCarrera.getByRole("button", { name: "Guardar" }).click();
    await page.getByRole("button", { name: "Nuevo plan" }).click();
    await page.getByRole("form", { name: "Nuevo plan" }).getByLabel("Resolución").fill("Res. 98/26");
    await page.getByRole("form", { name: "Nuevo plan" }).getByRole("button", { name: "Guardar" }).click();

    const agregarMateria = page.getByRole("form", { name: "Agregar materia" });
    for (const [nombre, anio] of [["Álgebra I", "1"], ["Álgebra II", "2"]]) {
      await agregarMateria.getByLabel("Materia").fill(nombre);
      await agregarMateria.getByLabel("Año").selectOption(anio);
      await agregarMateria.getByRole("button", { name: "Guardar" }).click();
      await expect(page.getByText(nombre, { exact: true })).toBeVisible();
    }

    await page.getByRole("button", { name: "Correlatividades de Álgebra II" }).click();
    const editor = page.getByRole("region", { name: "Correlatividades de Álgebra II" });
    const formulario = editor.getByRole("form", { name: "Agregar requisito" });
    await formulario.getByLabel("Para").selectOption("Rendir");
    await formulario.getByLabel("Requisito").selectOption({ label: "1.º · Álgebra I" });
    await formulario.getByLabel("Condición").selectOption("Aprobada");
    await formulario.getByRole("button", { name: "Agregar" }).click();

    const paraRendir = editor.getByRole("region", { name: "Para rendir el final" });
    await expect(paraRendir.getByRole("listitem")).toContainText("Álgebra I");
    await expect(paraRendir.getByRole("listitem")).toContainText("Aprobada");

    await paraRendir.getByRole("button", { name: "Quitar Álgebra I" }).click();
    await expect(paraRendir).toContainText("No tiene correlatividades.");
  });
});
