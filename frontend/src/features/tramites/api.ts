import { apiFetch } from "@/lib/api-client";
import type { Pagina } from "@/features/academico/api";

export type EstadoTramite = "Pendiente" | "EnProceso" | "Rechazado" | "Listo";
export type TipoCampo = "Texto" | "TextoLargo" | "Fecha" | "Carrera";

export type CampoTramite = {
  clave: string;
  etiqueta: string;
  tipo: TipoCampo;
  obligatorio: boolean;
  largoMaximo: number;
  ayuda: string | null;
};

export type TramiteTipo = { id: string; codigo: string; nombre: string; descripcion: string; activo: boolean; campos: CampoTramite[] };

export type TramiteResumen = {
  id: string;
  numero: string;
  tipoId: string;
  tipoNombre: string;
  estado: EstadoTramite;
  fechaSolicitud: string;
  updatedAt: string;
  solicitanteEmail: string;
  solicitanteApellido: string | null;
  solicitanteNombre: string | null;
};

export type Persona = { id: string; email: string; nombre: string | null; apellido: string | null; legajo: string | null };

export type EventoTramite = {
  estadoAnterior: EstadoTramite | null;
  estadoNuevo: EstadoTramite;
  comentario: string | null;
  actor: string;
  fecha: string;
};

export type TramiteDetalle = {
  id: string;
  numero: string;
  tipoId: string;
  tipoNombre: string;
  estado: EstadoTramite;
  fechaSolicitud: string;
  updatedAt: string;
  observacionAdministrativa: string | null;
  solicitante: Persona;
  administradorAsignado: string | null;
  datos: { clave: string; etiqueta: string; valor: string }[];
  historial: EventoTramite[];
};

export const ETIQUETA_ESTADO: Record<EstadoTramite, string> = {
  Pendiente: "Pendiente",
  EnProceso: "En proceso",
  Rechazado: "Rechazado",
  Listo: "Listo",
};

/** Arma la query string con los filtros que tienen valor; `estado` puede repetirse. */
function consulta(filtro: { estados?: EstadoTramite[]; texto?: string; page?: number; pageSize?: number }): string {
  const parametros = new URLSearchParams();
  for (const estado of filtro.estados ?? []) parametros.append("estado", estado);
  if (filtro.texto) parametros.set("texto", filtro.texto);
  if (filtro.page) parametros.set("page", String(filtro.page));
  if (filtro.pageSize) parametros.set("pageSize", String(filtro.pageSize));
  const texto = parametros.toString();
  return texto ? `?${texto}` : "";
}

export const tramitesApi = {
  tipos: (token: string) => apiFetch<TramiteTipo[]>("/tramites/tipos", { token }),

  /** `claveIdempotencia` evita duplicados si el pedido se envía dos veces (doble clic, reintento). */
  solicitar: (token: string, datos: { tipoId: string; datos: Record<string, string>; claveIdempotencia: string }) =>
    apiFetch<TramiteDetalle>("/tramites", { method: "POST", token, body: datos }),

  mios: (token: string, page = 1) => apiFetch<Pagina<TramiteResumen>>(`/me/tramites${consulta({ page, pageSize: 10 })}`, { token }),

  detalle: (token: string, id: string) => apiFetch<TramiteDetalle>(`/tramites/${id}`, { token }),

  bandeja: (token: string, filtro: { estados?: EstadoTramite[]; texto?: string; page?: number }) =>
    apiFetch<Pagina<TramiteResumen>>(`/admin/tramites${consulta({ ...filtro, pageSize: 10 })}`, { token }),

  detalleAdmin: (token: string, id: string) => apiFetch<TramiteDetalle>(`/admin/tramites/${id}`, { token }),

  tomar: (token: string, id: string) => apiFetch<TramiteDetalle>(`/admin/tramites/${id}/tomar`, { method: "POST", token }),

  aprobar: (token: string, id: string, comentario: string | null) =>
    apiFetch<TramiteDetalle>(`/admin/tramites/${id}/aprobar`, { method: "POST", token, body: { comentario } }),

  rechazar: (token: string, id: string, motivo: string) =>
    apiFetch<TramiteDetalle>(`/admin/tramites/${id}/rechazar`, { method: "POST", token, body: { motivo } }),
};

/** "15/11/2026 14:30", en hora del instituto. */
export function fechaHora(iso: string): string {
  return new Intl.DateTimeFormat("es-AR", {
    dateStyle: "short",
    timeStyle: "short",
    timeZone: "America/Argentina/Buenos_Aires",
  }).format(new Date(iso));
}
