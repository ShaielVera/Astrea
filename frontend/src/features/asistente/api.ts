import { apiDescargar, apiFetch } from "@/lib/api-client";
import type { Pagina } from "@/features/academico/api";

export const LARGO_MAXIMO_PREGUNTA = 1000;

/** Un documento oficial (con versión y páginas) o datos actuales de Astrea (con la sección donde verlos). */
export type Fuente = {
  tipo: "Documento" | "DatosDeAstrea";
  documentoId: string | null;
  titulo: string;
  version: number | null;
  seccion: string | null;
  fuente: string | null;
  paginaDesde: number | null;
  paginaHasta: number | null;
  extracto: string;
  enlace: string | null;
};

export type Propuesta = {
  estado: "Pendiente" | "Confirmada" | "Cancelada";
  tipoNombre: string;
  resumen: { etiqueta: string; valor: string }[];
  tramiteId: string | null;
  numeroTramite: string | null;
  venceEl: string;
};

export type Mensaje = {
  id: string;
  rol: "Usuario" | "Asistente";
  contenido: string;
  fuentes: Fuente[];
  sinEvidencia: boolean;
  esAclaracion: boolean;
  propuesta: Propuesta | null;
  fecha: string;
};

export type Intercambio = { conversacionId: string; titulo: string; pregunta: Mensaje; respuesta: Mensaje };

export type ConversacionResumen = { id: string; titulo: string; fechaCreacion: string; fechaUltimoMensaje: string };

export type Conversacion = ConversacionResumen & { mensajes: Mensaje[] };

const BASE = "/asistente";

export const asistenteApi = {
  preguntar: (token: string, mensaje: string, conversacionId: string | null) =>
    apiFetch<Intercambio>(`${BASE}/preguntas`, { method: "POST", token, body: { mensaje, conversacionId } }),

  conversaciones: (token: string) => apiFetch<Pagina<ConversacionResumen>>(`${BASE}/conversaciones?pageSize=20`, { token }),

  conversacion: (token: string, id: string) => apiFetch<Conversacion>(`${BASE}/conversaciones/${id}`, { token }),

  eliminar: (token: string, id: string) => apiFetch<void>(`${BASE}/conversaciones/${id}`, { method: "DELETE", token }),

  /** Recién acá se crea el trámite (una sola vez aunque se confirme dos veces). */
  confirmar: (token: string, conversacionId: string, mensajeId: string) =>
    apiFetch<Mensaje>(`${BASE}/conversaciones/${conversacionId}/mensajes/${mensajeId}/confirmar`, { method: "POST", token }),

  cancelar: (token: string, conversacionId: string, mensajeId: string) =>
    apiFetch<Mensaje>(`${BASE}/conversaciones/${conversacionId}/mensajes/${mensajeId}/cancelar`, { method: "POST", token }),

  abrirFuente: (token: string, fuente: Fuente) =>
    apiDescargar(`${BASE}/fuentes/${fuente.documentoId}`, token, `${fuente.titulo} (v${fuente.version ?? 1}).pdf`),
};

/** "pág. 3" o "págs. 3–4". */
export function paginas(fuente: Fuente): string | null {
  if (fuente.paginaDesde === null || fuente.paginaHasta === null) return null;
  return fuente.paginaDesde === fuente.paginaHasta ? `pág. ${fuente.paginaDesde}` : `págs. ${fuente.paginaDesde}–${fuente.paginaHasta}`;
}
