import { apiDescargar, apiEnviarFormulario, apiFetch } from "@/lib/api-client";
import type { Pagina } from "@/features/academico/api";

export type TipoDocumentoRag = "Reglamento" | "PlanDeEstudio" | "Cronograma" | "Comunicado" | "Horario" | "Otro";
export type EstadoIndexacion = "Pendiente" | "Procesando" | "Indexado" | "Error" | "Reemplazado" | "Retirado";

export const TIPOS: Record<TipoDocumentoRag, string> = {
  Reglamento: "Reglamento",
  PlanDeEstudio: "Plan de estudio",
  Cronograma: "Cronograma o calendario",
  Comunicado: "Comunicado",
  Horario: "Horario de cursada (de una carrera)",
  Otro: "Otro",
};

export const ESTADOS: Record<EstadoIndexacion, string> = {
  Pendiente: "Pendiente",
  Procesando: "Procesando",
  Indexado: "Indexado",
  Error: "Error",
  Reemplazado: "Reemplazado",
  Retirado: "Retirado",
};

/** Mientras está en alguno de estos estados, la página vuelve a consultar hasta que termine. */
export const EN_PROCESO: EstadoIndexacion[] = ["Pendiente", "Procesando"];

export const PDF_MAXIMO_BYTES = 20 * 1024 * 1024;

export type DocumentoRag = {
  id: string;
  serieId: string;
  version: number;
  titulo: string;
  tipo: TipoDocumentoRag;
  fuente: string | null;
  fechaDocumento: string | null;
  fechaCarga: string;
  cargadoPor: string;
  estado: EstadoIndexacion;
  errorProcesamiento: string | null;
  intentos: number;
  cantidadPaginas: number | null;
  cantidadFragmentos: number | null;
  fechaIndexacion: string | null;
  /** Solo los horarios: la carrera a la que pertenecen. */
  carreraId: string | null;
};

export type DetalleDocumentoRag = { documento: DocumentoRag; versiones: DocumentoRag[] };

export type Fragmento = { numeroFragmento: number; contenido: string; seccion: string | null; paginaDesde: number; paginaHasta: number };

export type NuevoDocumento = {
  archivo: File;
  titulo: string;
  tipo: TipoDocumentoRag;
  fuente?: string;
  fechaDocumento?: string;
  nuevaVersionDe?: string;
  carreraId?: string;
};

function consulta(parametros: Record<string, string | number | undefined>): string {
  const query = new URLSearchParams();
  for (const [clave, valor] of Object.entries(parametros)) {
    if (valor !== undefined && valor !== "") query.set(clave, String(valor));
  }
  const texto = query.toString();
  return texto ? `?${texto}` : "";
}

const BASE = "/admin/documentos-rag";

export const documentosRagApi = {
  listar: (token: string, filtro: { estado?: EstadoIndexacion; texto?: string; page?: number; pageSize?: number }) =>
    apiFetch<Pagina<DocumentoRag>>(`${BASE}${consulta({ pageSize: 10, ...filtro })}`, { token }),

  obtener: (token: string, id: string) => apiFetch<DetalleDocumentoRag>(`${BASE}/${id}`, { token }),

  fragmentos: (token: string, id: string, page = 1) => apiFetch<Pagina<Fragmento>>(`${BASE}/${id}/fragmentos?page=${page}&pageSize=20`, { token }),

  cargar: (token: string, datos: NuevoDocumento) => {
    const formulario = new FormData();
    formulario.set("archivo", datos.archivo);
    formulario.set("titulo", datos.titulo);
    formulario.set("tipo", datos.tipo);
    if (datos.fuente) formulario.set("fuente", datos.fuente);
    if (datos.fechaDocumento) formulario.set("fechaDocumento", datos.fechaDocumento);
    if (datos.nuevaVersionDe) formulario.set("nuevaVersionDe", datos.nuevaVersionDe);
    if (datos.carreraId) formulario.set("carreraId", datos.carreraId);
    return apiEnviarFormulario<DocumentoRag>(BASE, token, formulario);
  },

  reindexar: (token: string, id: string) => apiFetch<DocumentoRag>(`${BASE}/${id}/reindexar`, { method: "POST", token }),

  retirar: (token: string, id: string) => apiFetch<DocumentoRag>(`${BASE}/${id}/retirar`, { method: "POST", token }),

  descargar: (token: string, documento: DocumentoRag) => apiDescargar(`${BASE}/${documento.id}/archivo`, token, `${documento.titulo}.pdf`),
};
