"use client";

import Link from "next/link";
import { useRouter } from "next/navigation";
import { useEffect, type ReactNode } from "react";
import { useAuth } from "./AuthProvider";
import { areaInicial, tieneRol, type CodigoRol } from "./tipos";

/**
 * Protege un área de la interfaz. Es solo una ayuda de navegación:
 * la seguridad real la aplica la API en cada endpoint.
 */
export function RequiereRol({ rol, children }: { rol: CodigoRol; children: ReactNode }) {
  const { sesion, perfil } = useAuth();
  const router = useRouter();

  const sinSesion = sesion.estado === "anonimo";

  useEffect(() => {
    if (sinSesion) router.replace("/login");
  }, [sinSesion, router]);

  if (sesion.estado === "cargando" || sinSesion) {
    return (
      <p role="status" className="px-4 py-12 text-center text-zinc-500">
        Cargando…
      </p>
    );
  }

  if (!tieneRol(perfil, rol)) {
    return (
      <section className="mx-auto flex max-w-md flex-col items-center gap-4 px-4 py-16 text-center">
        <h1 className="text-2xl font-semibold">No tenés acceso a esta sección</h1>
        <p className="text-zinc-600 dark:text-zinc-400">Tu cuenta no tiene los permisos necesarios.</p>
        <Link href={perfil ? areaInicial(perfil) : "/"} className="font-medium text-indigo-600 hover:underline">
          Volver a mi inicio
        </Link>
      </section>
    );
  }

  return children;
}
