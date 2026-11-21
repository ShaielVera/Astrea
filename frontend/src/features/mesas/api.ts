import { apiFetch } from "@/lib/api-client";
import type { Pagina } from "@/features/academico/api";

export type EstadoMesa = "Borrador" | "Publicada" | "Cancelada";
export type EstadoInscripcion = "SinFechas" | "Proxima" | "Abierta" | "Cerrada";

/** Mesa de examen. `fecha` (AAAA-MM-DD) y `hora` (HH:mm:ss) están en hora del instituto (Argentina). */
export type MesaExamen = {
  id: string;
  estado: EstadoMesa;
  turno: string;
  llamado: number;
  fecha: string;
  hora: string;
  inicio: string;
  aula: string | null;
  tribunal: string[];
  inscripcionDesde: string | null;
  inscripcionHasta: string | null;
  estadoInscripcion: EstadoInscripcion;
  observaciones: string | null;
  motivoCancelacion: string | null;
  planId: string;
  carreraId: string;
  carreraCodigo: string;
  carreraNombre: string;
  materiaId: string;
  materiaCodigo: string;
  materiaNombre: string;
  anioMateria: number;
};

export type FiltroMesas = {
  desde?: string;
  hasta?: string;
  carreraId?: string;
  /** Varias carreras juntas (las del perfil del estudiante). */
  carreras?: string[];
  materiaId?: string;
  texto?: string;
  estado?: EstadoMesa;
  page?: number;
  pageSize?: number;
};

export type DatosMesa = {
  planId: string;
  materiaId: string;
  turno: string;
  llamado: number;
  fecha: string;
  hora: string;
  aula: string | null;
  tribunal: string[];
  inscripcionDesde: string | null;
  inscripcionHasta: string | null;
  observaciones: string | null;
};

/** Arma la query string solo con los filtros que tienen valor (nunca se mandan nombres de columnas). */
function consulta(filtro: FiltroMesas): string {
  const parametros = new URLSearchParams();
  for (const [clave, valor] of Object.entries(filtro)) {
    if (Array.isArray(valor)) valor.forEach((item) => parametros.append(clave, String(item)));
    else if (valor !== undefined && valor !== "") parametros.set(clave, String(valor));
  }
  const texto = parametros.toString();
  return texto ? `?${texto}` : "";
}

export const mesasApi = {
  buscar: (token: string, filtro: FiltroMesas) => apiFetch<Pagina<MesaExamen>>(`/mesas${consulta(filtro)}`, { token }),

  buscarAdmin: (token: string, filtro: FiltroMesas) => apiFetch<Pagina<MesaExamen>>(`/admin/mesas${consulta(filtro)}`, { token }),

  crear: (token: string, datos: DatosMesa) => apiFetch<MesaExamen>("/admin/mesas", { method: "POST", token, body: datos }),

  actualizar: (token: string, mesaId: string, datos: DatosMesa) =>
    apiFetch<MesaExamen>(`/admin/mesas/${mesaId}`, { method: "PUT", token, body: datos }),

  publicar: (token: string, mesaId: string) => apiFetch<MesaExamen>(`/admin/mesas/${mesaId}/publicacion`, { method: "POST", token }),

  cancelar: (token: string, mesaId: string, motivo: string) =>
    apiFetch<MesaExamen>(`/admin/mesas/${mesaId}/cancelacion`, { method: "POST", token, body: { motivo } }),

  eliminar: (token: string, mesaId: string) => apiFetch<void>(`/admin/mesas/${mesaId}`, { method: "DELETE", token }),
};

/** "jueves 10 de diciembre de 2026". La fecha ya es del instituto: se interpreta sin convertir de zona. */
export function fechaLarga(fecha: string): string {
  const [anio, mes, dia] = fecha.split("-").map(Number);
  return new Intl.DateTimeFormat("es-AR", { weekday: "long", day: "numeric", month: "long", year: "numeric", timeZone: "UTC" }).format(
    new Date(Date.UTC(anio, mes - 1, dia)),
  );
}

/** "10/12" para fechas de inscripción. */
export function fechaCorta(fecha: string): string {
  const [, mes, dia] = fecha.split("-");
  return `${dia}/${mes}`;
}

/** "18:00:00" → "18:00". */
export const horaCorta = (hora: string) => hora.slice(0, 5);

/** Hoy en el instituto (AAAA-MM-DD), sin importar la zona horaria del navegador. */
export function hoyEnElInstituto(): string {
  return new Intl.DateTimeFormat("en-CA", { timeZone: "America/Argentina/Buenos_Aires" }).format(new Date());
}

/** Agrupa las mesas por día, respetando el orden que ya trae la API (fecha, hora, materia). */
export function agruparPorDia(mesas: MesaExamen[]): [string, MesaExamen[]][] {
  const grupos = new Map<string, MesaExamen[]>();
  for (const mesa of mesas) {
    grupos.set(mesa.fecha, [...(grupos.get(mesa.fecha) ?? []), mesa]);
  }
  return [...grupos.entries()];
}
