import { apiFetch } from "@/lib/api-client";
import type { Pagina } from "@/features/academico/api";

export type ValorAsistencia = "AsisteRegularmente" | "AsisteIrregularmente" | "NoAsiste";

export const VALORES: { valor: ValorAsistencia; etiqueta: string }[] = [
  { valor: "AsisteRegularmente", etiqueta: "Asiste regularmente" },
  { valor: "AsisteIrregularmente", etiqueta: "Asiste de forma irregular" },
  { valor: "NoAsiste", etiqueta: "No asiste" },
];

export const ETIQUETA_VALOR: Record<ValorAsistencia, string> = {
  AsisteRegularmente: "Asiste regularmente",
  AsisteIrregularmente: "Asiste de forma irregular",
  NoAsiste: "No asiste",
};

export type MateriaCursada = { planMateriaId: string; materia: string; anio: number; carreraId: string; carrera: string };

export type UsuarioEncontrado = { id: string; email: string; nombre: string; dni: string | null; esDocente: boolean };

export type Asignacion = { id: string; planMateriaId: string; materia: string; anio: number; carrera: string };

export type Docente = { id: string; nombre: string; email: string; materias: Asignacion[] };

export type Relevamiento = { id: string; titulo: string; cicloLectivo: number; fechaCierre: string; abierto: boolean; creadoEn: string };

export type EstudianteAConfirmar = { id: string; nombre: string; email: string; valor: ValorAsistencia | null; observacion: string | null };

export type MateriaAConfirmar = { planMateriaId: string; materia: string; anio: number; carrera: string; estudiantes: EstudianteAConfirmar[] };

export type PanelDocente = { relevamiento: Relevamiento; materias: MateriaAConfirmar[] };

export type Confirmacion = { estudianteId: string; valor: ValorAsistencia; observacion: string | null };

export type MateriaInforme = {
  materia: string;
  carrera: string;
  anio: number;
  valor: ValorAsistencia | null;
  observacion: string | null;
  docente: string | null;
  confirmadaEn: string | null;
};

export type EstudianteInforme = {
  id: string;
  nombre: string;
  email: string;
  dni: string | null;
  materias: MateriaInforme[];
  regular: number;
  irregular: number;
  noAsiste: number;
  sinConfirmar: number;
};

export type Informe = { relevamiento: Relevamiento; estudiantes: Pagina<EstudianteInforme>; dnisSinDatos: string[] };

export type MateriaAvance = { planMateriaId: string; materia: string; anio: number; carrera: string; inscriptos: number; confirmados: number; docentes: string[] };

export type Avance = { relevamiento: Relevamiento; inscripciones: number; confirmadas: number; materias: MateriaAvance[] };

export type MiConfirmacion = {
  relevamientoId: string;
  relevamiento: string;
  fechaCierre: string;
  materia: string;
  valor: ValorAsistencia;
  observacion: string | null;
  confirmadaEn: string;
};

export type MiAsistencia = { cicloLectivo: number; materias: MateriaCursada[]; confirmaciones: MiConfirmacion[] };

export const asistenciaApi = {
  // Estudiante
  miAsistencia: (token: string) => apiFetch<MiAsistencia>("/me/asistencia", { token }),
  inscribirse: (token: string, planMateriaId: string) =>
    apiFetch<MateriaCursada>("/me/cursada", { method: "POST", token, body: { planMateriaId } }),
  darseDeBaja: (token: string, planMateriaId: string) => apiFetch<void>(`/me/cursada/${planMateriaId}`, { method: "DELETE", token }),

  // Docente
  misMaterias: (token: string) => apiFetch<MateriaCursada[]>("/docente/materias", { token }),
  marcarMateria: (token: string, planMateriaId: string) => apiFetch<MateriaCursada>(`/docente/materias/${planMateriaId}`, { method: "PUT", token }),
  desmarcarMateria: (token: string, planMateriaId: string) => apiFetch<void>(`/docente/materias/${planMateriaId}`, { method: "DELETE", token }),
  relevamientosDocente: (token: string) => apiFetch<Relevamiento[]>("/docente/relevamientos", { token }),
  panel: (token: string, relevamientoId: string) => apiFetch<PanelDocente>(`/docente/relevamientos/${relevamientoId}`, { token }),
  confirmar: (token: string, relevamientoId: string, planMateriaId: string, confirmaciones: Confirmacion[]) =>
    apiFetch<number>(`/docente/relevamientos/${relevamientoId}/materias/${planMateriaId}/confirmaciones`, {
      method: "POST",
      token,
      body: { confirmaciones },
    }),

  // Secretaría y dirección
  candidatos: (token: string, texto: string) =>
    apiFetch<UsuarioEncontrado[]>(`/admin/docentes/candidatos?texto=${encodeURIComponent(texto)}`, { token }),
  docentes: (token: string) => apiFetch<Docente[]>("/admin/docentes", { token }),
  hacerDocente: (token: string, usuarioId: string) => apiFetch<void>(`/admin/docentes/${usuarioId}`, { method: "POST", token }),
  quitarDocente: (token: string, usuarioId: string) => apiFetch<void>(`/admin/docentes/${usuarioId}`, { method: "DELETE", token }),
  asignarMateria: (token: string, usuarioId: string, planMateriaId: string) =>
    apiFetch<Asignacion>(`/admin/docentes/${usuarioId}/materias`, { method: "POST", token, body: { planMateriaId } }),
  quitarAsignacion: (token: string, asignacionId: string) =>
    apiFetch<void>(`/admin/docentes/asignaciones/${asignacionId}`, { method: "DELETE", token }),

  relevamientos: (token: string) => apiFetch<Relevamiento[]>("/admin/asistencia/relevamientos", { token }),
  abrirRelevamiento: (token: string, titulo: string, fechaCierre: string) =>
    apiFetch<Relevamiento>("/admin/asistencia/relevamientos", { method: "POST", token, body: { titulo, fechaCierre } }),
  cerrarRelevamiento: (token: string, id: string) =>
    apiFetch<Relevamiento>(`/admin/asistencia/relevamientos/${id}/cierre`, { method: "POST", token }),
  avance: (token: string, id: string) => apiFetch<Avance>(`/admin/asistencia/relevamientos/${id}/avance`, { token }),
  /** Los DNI van en el cuerpo, nunca en la URL. */
  informe: (token: string, id: string, filtro: { texto?: string; dnis?: string; page?: number }) =>
    apiFetch<Informe>(`/admin/asistencia/relevamientos/${id}/informe`, { method: "POST", token, body: { ...filtro, pageSize: 50 } }),
};

/** "2027-06-30" → "30/6/2027" (sin pasar por Date, para no correr el día por la zona horaria). */
export function fechaCorta(dia: string): string {
  const [anio, mes, numero] = dia.slice(0, 10).split("-");
  return `${Number(numero)}/${Number(mes)}/${anio}`;
}
