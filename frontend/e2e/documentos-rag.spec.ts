import { expect, test } from "@playwright/test";
import { ingresarComoAdministrador } from "./sesiones";

/**
 * PDF mínimo pero válido, con una línea de texto por renglón (Helvetica, sin tildes para no depender de la codificación).
 * Se arma a mano para no guardar archivos binarios en el repositorio.
 */
function pdfConTexto(lineas: string[]): Buffer {
  const escapar = (texto: string) => texto.replace(/[\\()]/g, (c) => `\\${c}`);
  const contenido = `BT /F1 12 Tf 50 780 Td 16 TL ${lineas.map((linea) => `(${escapar(linea)}) Tj T*`).join(" ")} ET`;
  const objetos = [
    "<< /Type /Catalog /Pages 2 0 R >>",
    "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
    "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 595 842] /Contents 4 0 R /Resources << /Font << /F1 5 0 R >> >> >>",
    `<< /Length ${contenido.length} >>\nstream\n${contenido}\nendstream`,
    "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica /Encoding /WinAnsiEncoding >>",
  ];

  let pdf = "%PDF-1.4\n";
  const posiciones: number[] = [];
  objetos.forEach((objeto, i) => {
    posiciones.push(pdf.length);
    pdf += `${i + 1} 0 obj\n${objeto}\nendobj\n`;
  });
  const inicioXref = pdf.length;
  pdf += `xref\n0 ${objetos.length + 1}\n0000000000 65535 f \n`;
  pdf += posiciones.map((posicion) => `${String(posicion).padStart(10, "0")} 00000 n \n`).join("");
  pdf += `trailer\n<< /Size ${objetos.length + 1} /Root 1 0 R >>\nstartxref\n${inicioXref}\n%%EOF\n`;
  return Buffer.from(pdf, "latin1");
}

test.describe("Documentos del asistente", () => {
  test("la Secretaría carga un reglamento en PDF, se indexa y ve sus fragmentos", async ({ page }) => {
    const titulo = `Reglamento de mesas ${Math.random().toString(36).slice(2, 8)}`;
    await ingresarComoAdministrador(page);

    await page.getByRole("navigation", { name: "Secciones de administración" }).getByRole("link", { name: /Documentos del asistente/ }).click();
    await expect(page).toHaveURL(/\/admin\/documentos$/);

    const formulario = page.getByRole("form", { name: "Cargar documento" });
    await formulario.getByLabel(/^Archivo PDF/).setInputFiles({
      name: "reglamento.pdf",
      mimeType: "application/pdf",
      // La línea con el título hace único al archivo (Chromium y el celular corren contra la misma base).
      buffer: pdfConTexto(["CAPITULO I", "DE LAS MESAS DE EXAMEN", "La inscripcion a mesas cierra 48 horas antes del examen.", titulo]),
    });
    await formulario.getByLabel("Título").fill(titulo);
    await formulario.getByLabel("Fuente (opcional)").fill("Resolucion 4043/09");
    await formulario.getByRole("button", { name: "Cargar" }).click();
    await expect(page.getByRole("main").getByText(`Se cargó "${titulo}" (versión 1)`)).toBeVisible();

    // El procesador en segundo plano lo indexa; la página se actualiza sola.
    const tarjeta = page.getByRole("article", { name: titulo });
    await expect(tarjeta.getByRole("status", { name: "Estado: Indexado" })).toBeVisible({ timeout: 30_000 });
    await expect(tarjeta).toContainText("1 página, 1 fragmento");

    await tarjeta.getByRole("button", { name: "Ver fragmentos" }).click();
    const fragmentos = tarjeta.getByRole("list", { name: `Fragmentos de ${titulo}` });
    await expect(fragmentos).toContainText("cierra 48 horas antes");
    await expect(fragmentos).toContainText("CAPITULO I — DE LAS MESAS DE EXAMEN");
  });
});
