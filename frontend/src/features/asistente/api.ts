import { apiDescargar, apiFetch } from "@/lib/api-client";
import type { Pagina } from "@/features/academico/api";

export const LARGO_MAXIMO_PREGUNTA = 1000;

export type Fuente = {
  documentoId: string;
  titulo: string;
  version: number;
  seccion: string | null;
  fuente: string | null;
  paginaDesde: number;
  paginaHasta: number;
  extracto: string;
};

export type Mensaje = {
  id: string;
  rol: "Usuario" | "Asistente";
  contenido: string;
  fuentes: Fuente[];
  sinEvidencia: boolean;
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

  abrirFuente: (token: string, fuente: Fuente) =>
    apiDescargar(`${BASE}/fuentes/${fuente.documentoId}`, token, `${fuente.titulo} (v${fuente.version}).pdf`),
};

/** "pág. 3" o "págs. 3–4". */
export function paginas(fuente: Fuente): string {
  return fuente.paginaDesde === fuente.paginaHasta ? `pág. ${fuente.paginaDesde}` : `págs. ${fuente.paginaDesde}–${fuente.paginaHasta}`;
}
