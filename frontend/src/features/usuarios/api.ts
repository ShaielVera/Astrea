import { apiFetch } from "@/lib/api-client";
import type { Pagina } from "@/features/academico/api";
import type { Asignacion } from "@/features/asistencia/api";
import type { CodigoRol } from "@/features/auth/tipos";

/** Una cuenta de "Usuarios y roles". Las materias solo vienen si es docente. */
export type Cuenta = {
  id: string;
  nombre: string;
  email: string;
  dni: string | null;
  roles: CodigoRol[];
  ultimoIngresoEn: string | null;
  creadaEn: string;
  activa: boolean;
  materias: Asignacion[];
};

/** Los roles que se dan y se quitan desde Administración (el de estudiante lo tiene toda cuenta). */
export type RolEditable = "ADMINISTRADOR" | "DOCENTE";

export const usuariosApi = {
  listar: (token: string, texto: string, page: number) => {
    const query = new URLSearchParams({ page: String(page), pageSize: "20" });
    if (texto.trim()) query.set("texto", texto.trim());
    return apiFetch<Pagina<Cuenta>>(`/admin/usuarios?${query}`, { token });
  },
  darRol: (token: string, usuarioId: string, rol: RolEditable) => apiFetch<void>(`/admin/usuarios/${usuarioId}/roles/${rol}`, { method: "PUT", token }),
  quitarRol: (token: string, usuarioId: string, rol: RolEditable) => apiFetch<void>(`/admin/usuarios/${usuarioId}/roles/${rol}`, { method: "DELETE", token }),
};

/** "hace 5 minutos", "ayer", "21/11/2026"… Para saber de un vistazo quién entró hace poco. */
export function haceCuanto(fecha: string | null, ahora = Date.now()): string {
  // Cuentas que no volvieron a entrar desde que Astrea empezó a anotar los ingresos.
  if (!fecha) return "Sin registro";
  const minutos = Math.floor((ahora - new Date(fecha).getTime()) / 60_000);
  if (minutos < 1) return "Recién";
  if (minutos < 60) return `Hace ${minutos} ${minutos === 1 ? "minuto" : "minutos"}`;
  const horas = Math.floor(minutos / 60);
  if (horas < 24) return `Hace ${horas} ${horas === 1 ? "hora" : "horas"}`;
  const dias = Math.floor(horas / 24);
  if (dias === 1) return "Ayer";
  if (dias < 7) return `Hace ${dias} días`;
  return new Date(fecha).toLocaleDateString("es-AR", { day: "numeric", month: "numeric", year: "numeric" });
}
