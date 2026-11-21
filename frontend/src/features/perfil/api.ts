import { apiFetch } from "@/lib/api-client";

export type Canal = "email" | "celular";

/** Un dato de contacto y su validación: dura un año y después hay que renovarla. */
export type Contacto = {
  valor: string | null;
  verificado: boolean;
  verificadoEl: string | null;
  venceEl: string | null;
  vencido: boolean;
};

export type MiPerfil = {
  emailCuenta: string;
  nombre: string | null;
  apellido: string | null;
  dni: string | null;
  legajo: string | null;
  localidad: string | null;
  carrerasIds: string[];
  emailContacto: Contacto;
  celular: Contacto;
  completo: boolean;
};

export type Localidad = { nombre: string; departamento: string; otrosNombres: string[] };

export type DatosPersonales = { nombre: string; apellido: string; dni: string; localidad: string | null; carrerasIds: string[] };

/** `codigoDemostracion`: solo en modo demostración (nunca en producción), para mostrarlo en pantalla. */
export type CodigoEnviado = { destino: string; venceEl: string; simulado: boolean; codigoDemostracion: string | null };

export const perfilApi = {
  obtener: (token: string) => apiFetch<MiPerfil>("/me/perfil", { token }),

  localidades: (token: string) => apiFetch<Localidad[]>("/localidades", { token }),

  guardarDatos: (token: string, datos: DatosPersonales) =>
    apiFetch<MiPerfil>("/me/perfil/datos", { method: "PUT", token, body: datos }),

  guardarContacto: (token: string, datos: { emailContacto: string | null; celular: string | null }) =>
    apiFetch<MiPerfil>("/me/perfil/contacto", { method: "PUT", token, body: datos }),

  enviarCodigo: (token: string, canal: Canal) => apiFetch<CodigoEnviado>(`/me/perfil/verificaciones/${canal}`, { method: "POST", token }),

  validarCodigo: (token: string, canal: Canal, codigo: string) =>
    apiFetch<MiPerfil>(`/me/perfil/verificaciones/${canal}/confirmacion`, { method: "POST", token, body: { codigo } }),
};

/** "3424123456" → "342 412-3456" (solo para mostrar). */
export function celularLegible(celular: string): string {
  return celular.length === 10 ? `${celular.slice(0, 3)} ${celular.slice(3, 6)}-${celular.slice(6)}` : celular;
}

/** Qué le falta al perfil, en palabras (vacío si está completo). */
export function pendientesDelPerfil(perfil: MiPerfil): string[] {
  const pendientes: string[] = [];
  if (!perfil.nombre || !perfil.apellido || !perfil.dni) pendientes.push("tu nombre, apellido y DNI");
  if (!perfil.localidad) pendientes.push("tu localidad");
  if (perfil.carrerasIds.length === 0) pendientes.push("la carrera que cursás");
  if (!perfil.emailContacto.valor) pendientes.push("un email de contacto");
  else if (!perfil.emailContacto.verificado) pendientes.push("validar tu email");
  else if (perfil.emailContacto.vencido) pendientes.push("renovar la validación de tu email (pasó un año)");
  if (!perfil.celular.valor) pendientes.push("tu celular");
  else if (perfil.celular.vencido) pendientes.push("renovar la validación de tu celular (pasó un año)");
  return pendientes;
}
