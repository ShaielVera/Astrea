/**
 * PDF mínimo pero válido, con una línea de texto por renglón (Helvetica, sin tildes para no depender de la codificación).
 * Se arma a mano para no guardar archivos binarios en el repositorio.
 */
export function pdfConTexto(lineas: string[]): Buffer {
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
