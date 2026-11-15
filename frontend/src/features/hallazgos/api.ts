import { apiEnviarArchivo, apiFetch, apiLeerArchivo } from "@/lib/api-client";
import type { Pagina } from "@/features/academico/api";

export type CategoriaObjeto =
  | "Electronica"
  | "Documentacion"
  | "Llaves"
  | "Ropa"
  | "Utiles"
  | "TermoOBotella"
  | "Accesorios"
  | "Herramientas"
  | "Otro";
export type PuntoResguardo = "Direccion" | "Secretaria" | "Biblioteca" | "AulaInfraestructura" | "Deposito";
export type FranjaHoraria = "Manana" | "Tarde" | "Noche";
export type EstadoObjeto = "Publicado" | "Entregado" | "DadoDeBaja";

export const CATEGORIAS: Record<CategoriaObjeto, string> = {
  Electronica: "Electrónica (celular, cargador, auriculares…)",
  Documentacion: "Documentación (DNI, tarjetas, carpetas)",
  Llaves: "Llaves",
  Ropa: "Ropa",
  Utiles: "Útiles y libros",
  TermoOBotella: "Termo o botella",
  Accesorios: "Accesorios (anteojos, billetera, mochila…)",
  Herramientas: "Herramientas y equipos",
  Otro: "Otro",
};

export const PUNTOS: Record<PuntoResguardo, string> = {
  Direccion: "Dirección",
  Secretaria: "Secretaría",
  Biblioteca: "Biblioteca",
  AulaInfraestructura: "Aula de Infraestructura",
  Deposito: "Depósito",
};

export const FRANJAS: Record<FranjaHoraria, string> = { Manana: "Mañana", Tarde: "Tarde", Noche: "Noche" };

export const ESTADOS: Record<EstadoObjeto, string> = { Publicado: "Publicado", Entregado: "Entregado", DadoDeBaja: "Dado de baja" };

/** Lo que ve cualquiera: sin descripción privada ni datos de quien lo publicó. */
export type HallazgoPublico = {
  id: string;
  tituloPublico: string;
  categoria: CategoriaObjeto;
  sectorEncontrado: string;
  franjaHoraria: FranjaHoraria;
  puntoResguardo: PuntoResguardo;
  fechaHallazgo: string;
  fechaPublicacion: string;
  tieneFoto: boolean;
};

export type MiHallazgo = HallazgoPublico & { descripcionPrivada: string; estado: EstadoObjeto; fechaCierre: string | null };

export type HallazgoAdmin = MiHallazgo & {
  publicadoPor: { id: string; email: string; nombre: string | null; apellido: string | null };
  entrega: {
    nombreRetirante: string;
    dniRetirante: string;
    identidadVerificada: boolean;
    verificadoPor: string;
    observacionInterna: string | null;
    fechaEntrega: string;
  } | null;
  motivoBaja: string | null;
  dadoDeBajaPor: string | null;
};

export type NuevoHallazgo = {
  tituloPublico: string;
  descripcionPrivada: string;
  categoria: CategoriaObjeto;
  sectorEncontrado: string;
  franjaHoraria: FranjaHoraria;
  puntoResguardo: PuntoResguardo;
  fechaHallazgo: string;
};

export const FOTO_TIPOS = ["image/jpeg", "image/png", "image/webp"];
export const FOTO_MAXIMO_BYTES = 5 * 1024 * 1024;

function consulta(parametros: Record<string, string | number | undefined>): string {
  const query = new URLSearchParams();
  for (const [clave, valor] of Object.entries(parametros)) {
    if (valor !== undefined && valor !== "") query.set(clave, String(valor));
  }
  const texto = query.toString();
  return texto ? `?${texto}` : "";
}

export const hallazgosApi = {
  buscar: (token: string, filtro: { texto?: string; categoria?: CategoriaObjeto; punto?: PuntoResguardo; page?: number }) =>
    apiFetch<Pagina<HallazgoPublico>>(`/hallazgos${consulta({ ...filtro, pageSize: 12 })}`, { token }),

  publicar: (token: string, datos: NuevoHallazgo) => apiFetch<MiHallazgo>("/hallazgos", { method: "POST", token, body: datos }),

  subirFoto: (token: string, id: string, foto: File) => apiEnviarArchivo<MiHallazgo>(`/hallazgos/${id}/foto`, token, foto),

  foto: (token: string, id: string) => apiLeerArchivo(`/hallazgos/${id}/foto`, token),

  mios: (token: string) => apiFetch<Pagina<MiHallazgo>>("/me/hallazgos?pageSize=50", { token }),

  buscarAdmin: (token: string, filtro: { estado?: EstadoObjeto; texto?: string; page?: number }) =>
    apiFetch<Pagina<HallazgoAdmin>>(`/admin/hallazgos${consulta({ ...filtro, pageSize: 10 })}`, { token }),

  entregar: (token: string, id: string, datos: { nombreRetirante: string; dniRetirante: string; identidadVerificada: boolean; observacionInterna: string | null }) =>
    apiFetch<HallazgoAdmin>(`/admin/hallazgos/${id}/entrega`, { method: "POST", token, body: datos }),

  darDeBaja: (token: string, id: string, motivo: string) =>
    apiFetch<HallazgoAdmin>(`/admin/hallazgos/${id}/baja`, { method: "POST", token, body: { motivo } }),
};

/** "15/11/2026" a partir de "2026-11-15" (fecha del instituto, sin convertir zona). */
export function fechaCorta(fecha: string): string {
  const [anio, mes, dia] = fecha.split("-");
  return `${dia}/${mes}/${anio}`;
}
