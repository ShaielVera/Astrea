import { apiFetch } from "@/lib/api-client";

export type Pagina<T> = {
  items: T[];
  page: number;
  pageSize: number;
  totalItems: number;
  totalPages: number;
  hasPrevious: boolean;
  hasNext: boolean;
};

export type Cuatrimestre = "Anual" | "Primero" | "Segundo";

export type Carrera = { id: string; codigo: string; nombre: string; duracionAnios: number; activa: boolean };

export type PlanEstudio = {
  id: string;
  carreraId: string;
  codigoVersion: string;
  nombre: string;
  vigenciaDesde: string | null;
  vigenciaHasta: string | null;
  activo: boolean;
  cantidadMaterias: number;
};

export type MateriaDePlan = {
  planMateriaId: string;
  materiaId: string;
  codigo: string;
  nombre: string;
  anio: number;
  cuatrimestre: Cuatrimestre;
  orden: number;
  cargaHorariaSemanal: number | null;
  obligatoria: boolean;
  activa: boolean;
};

export type TipoCorrelatividad = "Cursar" | "Rendir";
export type CondicionRequisito = "Regularizada" | "Aprobada";
export type EstadoMateria = "SinCursar" | "Regularizada" | "Aprobada";
export type ResultadoVerificacion = "Cumple" | "NoCumple" | "RequiereVerificacion";

/**
 * Requisito de correlatividad. Sin `materiaId` es una condición en texto que verifica Bedelía.
 * Los requisitos con el mismo `grupoAlternativa` son alternativas (alcanza con uno); sin grupo, obligatorios.
 */
export type Requisito = {
  id: string;
  condicion: CondicionRequisito;
  grupoAlternativa: number | null;
  materiaId: string | null;
  codigo: string | null;
  nombre: string | null;
  anio: number | null;
  descripcion: string | null;
};

export type CorrelatividadesDeMateria = {
  planId: string;
  planMateriaId: string;
  materiaId: string;
  codigo: string;
  nombre: string;
  anio: number;
  paraCursar: Requisito[];
  paraRendir: Requisito[];
};

export type Verificacion = {
  tipo: TipoCorrelatividad;
  resultado: ResultadoVerificacion;
  faltantes: Requisito[];
  aVerificar: Requisito[];
};

export type NuevaCorrelatividad = {
  materiaDestinoId: string;
  tipo: TipoCorrelatividad;
  materiaRequisitoId: string | null;
  condicion: CondicionRequisito;
  grupoAlternativa: number | null;
  descripcion: string | null;
};

export const ETIQUETA_CUATRIMESTRE: Record<Cuatrimestre, string> = {
  Anual: "Anual",
  Primero: "1.º cuatrimestre",
  Segundo: "2.º cuatrimestre",
};

export const NOMBRE_ANIO = ["", "Primer año", "Segundo año", "Tercer año", "Cuarto año", "Quinto año", "Sexto año"];

/** Prefijo de rutas: el panel de administración ve también lo inactivo. */
type Alcance = "estudiante" | "admin";
const prefijo = (alcance: Alcance) => (alcance === "admin" ? "/admin" : "");

/** Recorre todas las páginas: un listado de carreras o materias de un plan nunca es muy largo. */
async function todasLasPaginas<T>(ruta: string, token: string): Promise<T[]> {
  const items: T[] = [];
  for (let page = 1; ; page++) {
    const separador = ruta.includes("?") ? "&" : "?";
    const pagina = await apiFetch<Pagina<T>>(`${ruta}${separador}page=${page}&pageSize=50`, { token });
    items.push(...pagina.items);
    if (!pagina.hasNext) return items;
  }
}

export const academicoApi = {
  carreras: (token: string, alcance: Alcance = "estudiante") =>
    todasLasPaginas<Carrera>(`${prefijo(alcance)}/carreras`, token),

  planes: (token: string, carreraId: string, alcance: Alcance = "estudiante") =>
    apiFetch<PlanEstudio[]>(`${prefijo(alcance)}/carreras/${carreraId}/planes`, { token }),

  materias: (token: string, planId: string, alcance: Alcance = "estudiante") =>
    todasLasPaginas<MateriaDePlan>(`${prefijo(alcance)}/planes/${planId}/materias`, token),

  crearCarrera: (token: string, datos: { codigo: string; nombre: string; duracionAnios: number }) =>
    apiFetch<Carrera>("/admin/carreras", { method: "POST", token, body: datos }),

  actualizarCarrera: (token: string, carrera: Carrera) =>
    apiFetch<Carrera>(`/admin/carreras/${carrera.id}`, { method: "PUT", token, body: carrera }),

  crearPlan: (token: string, carreraId: string, datos: { codigoVersion: string; nombre: string }) =>
    apiFetch<PlanEstudio>(`/admin/carreras/${carreraId}/planes`, { method: "POST", token, body: datos }),

  agregarMateria: (
    token: string,
    planId: string,
    datos: { nombre: string; anio: number; cuatrimestre: Cuatrimestre; cargaHorariaSemanal: number | null },
  ) => apiFetch<MateriaDePlan>(`/admin/planes/${planId}/materias`, { method: "POST", token, body: datos }),

  actualizarMateria: (token: string, planId: string, materia: MateriaDePlan) =>
    apiFetch<MateriaDePlan>(`/admin/planes/${planId}/materias/${materia.planMateriaId}`, { method: "PUT", token, body: materia }),

  correlatividades: (token: string, planId: string, materiaId: string, alcance: Alcance = "estudiante") =>
    apiFetch<CorrelatividadesDeMateria>(`${prefijo(alcance)}/planes/${planId}/materias/${materiaId}/correlatividades`, { token }),

  verificarCorrelatividades: (
    token: string,
    planId: string,
    materiaId: string,
    tipo: TipoCorrelatividad,
    estados: { materiaId: string; estado: EstadoMateria }[],
  ) =>
    apiFetch<Verificacion>(`/planes/${planId}/materias/${materiaId}/correlatividades/verificacion`, {
      method: "POST",
      token,
      body: { tipo, estados },
    }),

  agregarCorrelatividad: (token: string, planId: string, datos: NuevaCorrelatividad) =>
    apiFetch<CorrelatividadesDeMateria>(`/admin/planes/${planId}/correlatividades`, { method: "POST", token, body: datos }),

  quitarCorrelatividad: (token: string, planId: string, correlatividadId: string) =>
    apiFetch<void>(`/admin/planes/${planId}/correlatividades/${correlatividadId}`, { method: "DELETE", token }),
};

/** Agrupa las materias por año, respetando el orden del plan. */
export function agruparPorAnio(materias: MateriaDePlan[]): [number, MateriaDePlan[]][] {
  const grupos = new Map<number, MateriaDePlan[]>();
  for (const materia of materias) {
    grupos.set(materia.anio, [...(grupos.get(materia.anio) ?? []), materia]);
  }
  return [...grupos.entries()].sort(([a], [b]) => a - b);
}
