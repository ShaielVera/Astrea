export const ROLES = {
  estudiante: "ESTUDIANTE",
  administrador: "ADMINISTRADOR",
} as const;

export type CodigoRol = (typeof ROLES)[keyof typeof ROLES];

export type Perfil = {
  id: string;
  email: string;
  nombre: string | null;
  apellido: string | null;
  roles: CodigoRol[];
};

export function tieneRol(perfil: Perfil | null, rol: CodigoRol): boolean {
  return perfil?.roles.includes(rol) ?? false;
}

/** Área a la que se redirige al iniciar sesión según los roles. */
export function areaInicial(perfil: Perfil): string {
  return tieneRol(perfil, ROLES.administrador) ? "/admin" : "/inicio";
}
