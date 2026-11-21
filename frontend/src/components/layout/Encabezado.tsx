"use client";

import Link from "next/link";
import { Suspense } from "react";
import { usePathname, useRouter } from "next/navigation";
import { useAuth } from "@/features/auth/AuthProvider";
import { ROLES, tieneRol, type CodigoRol, type Perfil } from "@/features/auth/tipos";

type Enlace = { href: string; texto: string; rol: CodigoRol };

const ENLACES: Enlace[] = [
  { href: "/inicio", texto: "Mi inicio", rol: ROLES.estudiante },
  { href: "/plan-de-estudios", texto: "Plan de estudios", rol: ROLES.estudiante },
  { href: "/correlatividades", texto: "Correlatividades", rol: ROLES.estudiante },
  { href: "/mesas", texto: "Mesas de examen", rol: ROLES.estudiante },
  { href: "/tramites", texto: "Mis trámites", rol: ROLES.estudiante },
  { href: "/hallazgos", texto: "Hallazgos", rol: ROLES.estudiante },
  { href: "/mi-cursada", texto: "Mi cursada", rol: ROLES.estudiante },
  { href: "/asistente", texto: "Asistente", rol: ROLES.estudiante },
  { href: "/docente", texto: "Confirmar asistencia", rol: ROLES.docente },
  { href: "/admin", texto: "Administración", rol: ROLES.administrador },
];

function iniciales(perfil: Perfil): string {
  const letras = [perfil.nombre, perfil.apellido].map((parte) => parte?.trim()[0]).filter(Boolean);
  return (letras.length > 0 ? letras.join("") : perfil.email[0]).toUpperCase();
}

export function Encabezado() {
  const { sesion, perfil, cerrarSesion } = useAuth();
  const router = useRouter();

  async function salir() {
    await cerrarSesion();
    router.replace("/login");
  }

  const enlaces = perfil ? ENLACES.filter((enlace) => tieneRol(perfil, enlace.rol)) : [];

  return (
    <header className="border-b-[3px] border-dorado-500 bg-gradient-to-b from-indigo-800 to-indigo-950 text-white shadow-md">
      <div className="mx-auto flex w-full max-w-5xl flex-col px-4 sm:px-6">
        <div className="flex items-center justify-between gap-3 py-3">
          <Link href={sesion.estado === "autenticado" && perfil ? "/inicio" : "/"} aria-label="Astrea, ir al inicio" className="flex items-center gap-3 rounded-md focus-visible:outline-2 focus-visible:outline-dorado-300">
            {/* eslint-disable-next-line @next/next/no-img-element -- SVG estático, no necesita optimización */}
            <img src="/icons/astrea.svg" alt="" width={44} height={44} className="size-11 rounded-xl ring-1 ring-dorado-500/60" />
            <span className="flex flex-col leading-tight">
              <span className="font-serif text-xl font-semibold">Astrea</span>
              <span className="text-[0.68rem] font-semibold uppercase tracking-[0.16em] text-indigo-200"><span className="hidden sm:inline">Portal académico · </span>ISP N.º 20</span>
            </span>
          </Link>

          {sesion.estado === "autenticado" && perfil ? (
            <div className="flex items-center gap-2">
              <Link
                href="/mi-perfil"
                aria-label="Mi perfil"
                title={`Mi perfil (${perfil.email})`}
                className="flex size-10 items-center justify-center rounded-full bg-dorado-500 text-sm font-bold text-indigo-950 ring-2 ring-dorado-300/50 hover:bg-dorado-400 focus-visible:outline-2 focus-visible:outline-dorado-300"
              >
                {iniciales(perfil)}
              </Link>
              <span className="sr-only" data-testid="email-usuario">
                {perfil.email}
              </span>
              <button
                type="button"
                onClick={salir}
                aria-label="Cerrar sesión"
                title="Cerrar sesión"
                className="flex size-10 items-center justify-center rounded-full border border-white/30 text-white hover:bg-white/10 focus-visible:outline-2 focus-visible:outline-dorado-300"
              >
                <svg aria-hidden="true" viewBox="0 0 24 24" className="size-[18px]" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
                  <path d="M9 21H5a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2h4" />
                  <path d="m16 17 5-5-5-5" />
                  <path d="M21 12H9" />
                </svg>
              </button>
            </div>
          ) : (
            sesion.estado === "anonimo" && (
              <Link href="/login" className="rounded-full bg-dorado-500 px-4 py-2 text-sm font-semibold text-indigo-950 hover:bg-dorado-400">
                Ingresar
              </Link>
            )
          )}
        </div>

        {enlaces.length > 0 && (
          // usePathname suspende en rutas con parámetros dinámicos (Cache Components): mientras tanto, la misma lista sin marcar.
          <Suspense fallback={<Navegacion enlaces={enlaces} ruta={null} />}>
            <NavegacionConRuta enlaces={enlaces} />
          </Suspense>
        )}
      </div>
    </header>
  );
}

function NavegacionConRuta({ enlaces }: { enlaces: Enlace[] }) {
  return <Navegacion enlaces={enlaces} ruta={usePathname()} />;
}

function Navegacion({ enlaces, ruta }: { enlaces: Enlace[]; ruta: string | null }) {
  return (
    <nav aria-label="Principal" className="border-t border-white/15 py-2.5">
      <ul className="flex flex-wrap justify-center gap-1 text-sm">
        {enlaces.map((enlace) => {
          const activo = ruta !== null && (ruta === enlace.href || ruta.startsWith(`${enlace.href}/`));
          return (
            <li key={enlace.href}>
              <Link
                href={enlace.href}
                aria-current={activo ? "page" : undefined}
                className={
                  activo
                    ? "block rounded-full bg-dorado-500 px-3.5 py-1.5 font-semibold text-indigo-950"
                    : "block rounded-full px-3.5 py-1.5 font-medium text-indigo-100 hover:bg-white/10 hover:text-white"
                }
              >
                {enlace.texto}
              </Link>
            </li>
          );
        })}
      </ul>
    </nav>
  );
}
