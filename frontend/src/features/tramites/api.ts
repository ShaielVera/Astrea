import { apiDescargar, apiFetch } from "@/lib/api-client";
import type { Pagina } from "@/features/academico/api";

export type EstadoTramite = "Pendiente" | "EnProceso" | "Rechazado" | "Listo" | "Cancelado";
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

export type Persona = {
  id: string;
  email: string;
  nombre: string | null;
  apellido: string | null;
  legajo: string | null;
  dni: string | null;
  /** Secretaría ya revisó nombre, apellido y DNI (si no, los cargó el estudiante en "Mi perfil"). */
  datosConfirmados: boolean;
  emailContacto: string | null;
  emailContactoVerificadoEn: string | null;
  celular: string | null;
  celularVerificadoEn: string | null;
};

export type EstadoDocumento = "Vigente" | "Anulado";

/** PDF oficial emitido al aprobar el trámite. */
export type Documento = {
  id: string;
  tipoDocumento: string;
  codigoValidacion: string;
  urlValidacion: string;
  fechaEmision: string;
  estado: EstadoDocumento;
  versionPlantilla: number;
  hashSha256: string;
  motivoAnulacion: string | null;
};

/** Lo que muestra la validación pública del QR. */
export type ValidacionDocumento = {
  codigoValidacion: string;
  tipoDocumento: string;
  numeroTramite: string;
  fechaEmision: string;
  estado: EstadoDocumento;
  dniParcial: string | null;
  hashSha256: string;
};

export type PlantillaDeTipo = {
  tipoId: string;
  tipoNombre: string;
  vigente: { id: string; version: number; titulo: string; cuerpo: string; creadaAt: string } | null;
  marcadoresDisponibles: string[];
};

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
  /** El tipo emite un PDF al aprobarse (tiene plantilla). */
  emiteDocumento: boolean;
  documento: Documento | null;
};

export const ETIQUETA_ESTADO: Record<EstadoTramite, string> = {
  Pendiente: "Pendiente",
  EnProceso: "En proceso",
  Rechazado: "Rechazado",
  Listo: "Listo",
  Cancelado: "Cancelado",
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

  /** El titular lo cancela mientras esté pendiente o en proceso. */
  cancelar: (token: string, id: string) => apiFetch<TramiteDetalle>(`/tramites/${id}/cancelacion`, { method: "POST", token }),

  bandeja: (token: string, filtro: { estados?: EstadoTramite[]; texto?: string; page?: number }) =>
    apiFetch<Pagina<TramiteResumen>>(`/admin/tramites${consulta({ ...filtro, pageSize: 10 })}`, { token }),

  detalleAdmin: (token: string, id: string) => apiFetch<TramiteDetalle>(`/admin/tramites/${id}`, { token }),

  tomar: (token: string, id: string) => apiFetch<TramiteDetalle>(`/admin/tramites/${id}/tomar`, { method: "POST", token }),

  aprobar: (token: string, id: string, comentario: string | null) =>
    apiFetch<TramiteDetalle>(`/admin/tramites/${id}/aprobar`, { method: "POST", token, body: { comentario } }),

  rechazar: (token: string, id: string, motivo: string) =>
    apiFetch<TramiteDetalle>(`/admin/tramites/${id}/rechazar`, { method: "POST", token, body: { motivo } }),

  descargar: (token: string, id: string) => apiDescargar(`/tramites/${id}/documento`, token, "documento.pdf"),

  descargarAdmin: (token: string, id: string) => apiDescargar(`/admin/tramites/${id}/documento`, token, "documento.pdf"),

  anularDocumento: (token: string, id: string, motivo: string) =>
    apiFetch<TramiteDetalle>(`/admin/tramites/${id}/documento/anulacion`, { method: "POST", token, body: { motivo } }),

  datosOficiales: (token: string, usuarioId: string, datos: { nombre: string; apellido: string; dni: string; legajo: string | null }) =>
    apiFetch<Persona>(`/admin/usuarios/${usuarioId}/datos-oficiales`, { method: "PUT", token, body: datos }),

  plantillas: (token: string) => apiFetch<PlantillaDeTipo[]>("/admin/plantillas", { token }),

  nuevaVersionPlantilla: (token: string, tipoId: string, datos: { titulo: string; cuerpo: string }) =>
    apiFetch<unknown>(`/admin/tramites/tipos/${tipoId}/plantillas`, { method: "POST", token, body: datos }),

  /** Pública: no requiere sesión. */
  validar: (codigo: string) => apiFetch<ValidacionDocumento>(`/documentos/validar/${encodeURIComponent(codigo)}`),
};

/** "15/11/2026 14:30", en hora del instituto. */
export function fechaHora(iso: string): string {
  return new Intl.DateTimeFormat("es-AR", {
    dateStyle: "short",
    timeStyle: "short",
    timeZone: "America/Argentina/Buenos_Aires",
  }).format(new Date(iso));
}
