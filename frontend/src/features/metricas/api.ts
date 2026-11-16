import { apiFetch } from "@/lib/api-client";

export type Resumen = {
  total: number;
  respondidas: number;
  sinEvidencia: number;
  aclaraciones: number;
  errores: number;
  propuestasDeTramite: number;
  duracionPromedioMs: number;
  porcentajeRespondidas: number | null;
};

export type ConsultasDelDia = { dia: string; total: number; sinRespuesta: number };

export type Tema = { tema: string; cantidad: number; ultima: string };

export type Cantidad = { nombre: string; cantidad: number };

export type MetricasAsistente = {
  desde: string;
  hasta: string;
  resumen: Resumen;
  porDia: ConsultasDelDia[];
  frecuentes: Tema[];
  sinRespuesta: Tema[];
  herramientas: Cantidad[];
  motivos: Cantidad[];
};

export const metricasApi = {
  asistente: (token: string, desde?: string, hasta?: string) => {
    const parametros = new URLSearchParams();
    if (desde) parametros.set("desde", desde);
    if (hasta) parametros.set("hasta", hasta);
    const consulta = parametros.size > 0 ? `?${parametros}` : "";
    return apiFetch<MetricasAsistente>(`/admin/asistente/metricas${consulta}`, { token });
  },
};

export const HERRAMIENTAS: Record<string, string> = {
  buscar_correlatividades: "Correlatividades",
  buscar_mesas: "Mesas de examen",
  buscar_hallazgos: "Hallazgos",
  consultar_mis_tramites: "Mis trámites",
  proponer_tramite: "Propuestas de trámite",
  "(no permitida)": "Pedidos bloqueados (herramienta no permitida)",
};

export const MOTIVOS: Record<string, string> = {
  SinInformacion: "No había información en los documentos ni en los datos",
  CitasInvalidas: "Respuesta descartada por no citar fuentes reales",
  RespuestaVacia: "La IA no devolvió una respuesta",
  PasosAgotados: "La IA no terminó de responder a tiempo",
  ProveedorNoDisponible: "La IA no estuvo disponible",
};

/** "2026-11-15" → "15/11" (sin pasar por Date, para no correr el día por la zona horaria). */
export function diaCorto(dia: string): string {
  const [, mes, numero] = dia.split("-");
  return `${Number(numero)}/${Number(mes)}`;
}

/** "2026-11-15" → "15/11/2026". */
export function diaLargo(dia: string): string {
  const [anio, mes, numero] = dia.split("-");
  return `${Number(numero)}/${Number(mes)}/${anio}`;
}

/** Hoy y hace n−1 días, en formato yyyy-mm-dd y hora local. */
export function ultimosDias(n: number): { desde: string; hasta: string } {
  const formato = (fecha: Date) =>
    `${fecha.getFullYear()}-${String(fecha.getMonth() + 1).padStart(2, "0")}-${String(fecha.getDate()).padStart(2, "0")}`;
  const hoy = new Date();
  const inicio = new Date(hoy.getFullYear(), hoy.getMonth(), hoy.getDate() - (n - 1));
  return { desde: formato(inicio), hasta: formato(hoy) };
}
