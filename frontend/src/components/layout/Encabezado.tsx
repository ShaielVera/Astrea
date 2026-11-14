"use client";

import Link from "next/link";
import { useRouter } from "next/navigation";
import { useAuth } from "@/features/auth/AuthProvider";
import { ROLES, tieneRol } from "@/features/auth/tipos";

export function Encabezado() {
  const { sesion, perfil, cerrarSesion } = useAuth();
  const router = useRouter();

  async function salir() {
    await cerrarSesion();
    router.replace("/login");
  }

  return (
    <header className="border-b border-zinc-200 dark:border-zinc-800">
      <div className="mx-auto flex w-full max-w-5xl flex-wrap items-center justify-between gap-3 px-4 py-3 sm:px-6">
        <Link href="/" className="text-lg font-semibold tracking-tight">
          Astrea
        </Link>

        {sesion.estado === "autenticado" && perfil ? (
          <nav aria-label="Principal" className="flex flex-wrap items-center gap-x-4 gap-y-2 text-sm">
            {tieneRol(perfil, ROLES.estudiante) && (
              <Link href="/inicio" className="hover:underline">
                Mi inicio
              </Link>
            )}
            {tieneRol(perfil, ROLES.administrador) && (
              <Link href="/admin" className="hover:underline">
                Administración
              </Link>
            )}
            <span className="max-w-[16rem] truncate text-zinc-500" data-testid="email-usuario">
              {perfil.email}
            </span>
            <button
              type="button"
              onClick={salir}
              className="rounded-md border border-zinc-300 px-3 py-1.5 font-medium hover:bg-zinc-100 dark:border-zinc-700 dark:hover:bg-zinc-800"
            >
              Cerrar sesión
            </button>
          </nav>
        ) : (
          sesion.estado === "anonimo" && (
            <Link
              href="/login"
              className="rounded-md bg-indigo-600 px-3 py-1.5 text-sm font-medium text-white hover:bg-indigo-700"
            >
              Ingresar
            </Link>
          )
        )}
      </div>
    </header>
  );
}
