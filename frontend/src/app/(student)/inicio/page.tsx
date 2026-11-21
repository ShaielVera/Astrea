"use client";

import Link from "next/link";
import { useEffect, useState, useSyncExternalStore, type ReactNode } from "react";
import { useAuth } from "@/features/auth/AuthProvider";
import { ROLES, tieneRol } from "@/features/auth/tipos";
import { pendientesDelPerfil, perfilApi } from "@/features/perfil/api";
import { AvisosDeTramites } from "@/features/tramites/AvisosDeTramites";

// Íconos de trazo simple (24×24), en la línea de los de Atenea.
const ICONOS: Record<string, ReactNode> = {
  plan: <path d="M4 19.5A2.5 2.5 0 0 1 6.5 17H20V3H6.5A2.5 2.5 0 0 0 4 5.5zM4 19.5A2.5 2.5 0 0 0 6.5 22H20v-5M8 7.5h8M8 11h5" />,
  correlatividades: (
    <>
      <circle cx="6" cy="6" r="2.5" />
      <circle cx="18" cy="18" r="2.5" />
      <path d="M8.5 6H14a4 4 0 0 1 4 4v5.5" />
    </>
  ),
  mesas: (
    <>
      <rect x="3" y="4.5" width="18" height="17" rx="2" />
      <path d="M8 2.5v4M16 2.5v4M3 10h18M8.5 15l2 2 4-4" />
    </>
  ),
  tramites: (
    <>
      <path d="M14 2.5H6.5A2 2 0 0 0 4.5 4.5v15a2 2 0 0 0 2 2h11a2 2 0 0 0 2-2V8z" />
      <path d="M14 2.5V8h5.5M8.5 13h7M8.5 17h5" />
    </>
  ),
  cursada: (
    <>
      <path d="M16 21v-2a4 4 0 0 0-4-4H6a4 4 0 0 0-4 4v2" />
      <circle cx="9" cy="7" r="4" />
      <path d="m16 11 2 2 4-4" />
    </>
  ),
  hallazgos: (
    <>
      <circle cx="11" cy="11" r="7" />
      <path d="m20.5 20.5-4.5-4.5" />
    </>
  ),
  perfil: (
    <>
      <circle cx="12" cy="8" r="4" />
      <path d="M4 21a8 8 0 0 1 16 0" />
    </>
  ),
  asistente: <path d="M21 15a2 2 0 0 1-2 2H7l-4 4V5a2 2 0 0 1 2-2h14a2 2 0 0 1 2 2zM8 9h8M8 13h5" />,
};

const modulos = [
  { titulo: "Mi perfil", descripcion: "Tus datos para las constancias, y tu email y celular validados.", href: "/mi-perfil", icono: "perfil" },
  { titulo: "Plan de estudios", descripcion: "Las materias de cada carrera, año por año.", href: "/plan-de-estudios", icono: "plan" },
  { titulo: "Correlatividades", descripcion: "Qué necesitás para cursar y para rendir cada materia.", href: "/correlatividades", icono: "correlatividades" },
  { titulo: "Mesas de examen", descripcion: "Llamados, fechas, horarios y profesores.", href: "/mesas", icono: "mesas" },
  { titulo: "Mis trámites", descripcion: "Pedí constancias y seguí su estado.", href: "/tramites", icono: "tramites" },
  { titulo: "Mi cursada", descripcion: "Anotá las materias que cursás y mirá tu asistencia confirmada.", href: "/mi-cursada", icono: "cursada" },
  { titulo: "Hallazgos", descripcion: "Objetos perdidos encontrados en el instituto.", href: "/hallazgos", icono: "hallazgos" },
  { titulo: "Asistente", descripcion: "Preguntá sobre reglamentos y calendarios, con fuentes oficiales.", href: "/asistente", icono: "asistente" },
];

/** Las otras páginas y aplicaciones del instituto (se abren en otra pestaña). */
const sitiosDelInstituto = [
  { titulo: "Instituto", descripcion: "Sitio oficial del ISP N.º 20.", href: "https://isp20.edu.ar/nuevo/" },
  { titulo: "Mesas", descripcion: "Inscripción a mesas y horarios de cursada de tu carrera.", href: "https://app.isp20.edu.ar/" },
  { titulo: "Ágora", descripcion: "Capacitaciones.", href: "https://agora.isp20.edu.ar/capacitaciones" },
  { titulo: "Atenea", descripcion: "Biblioteca del instituto: pedí libros.", href: "https://atenea.isp20.edu.ar/" },
  { titulo: "Préstamos", descripcion: "Préstamo de equipos y materiales.", href: "https://prestamos.isp20.edu.ar/" },
];

function saludo(hora: number): string {
  if (hora >= 5 && hora < 13) return "Buen día";
  if (hora >= 13 && hora < 20) return "Buenas tardes";
  return "Buenas noches";
}

type Momento = "manana" | "tarde" | "atardecer" | "noche";

/** El color del cielo del panel de bienvenida: blanco de día, durazno al atardecer y azul con estrellas de noche. */
function momentoDelDia(hora: number): Momento {
  if (hora >= 5 && hora < 13) return "manana";
  if (hora >= 13 && hora < 18) return "tarde";
  if (hora >= 18 && hora < 20) return "atardecer";
  return "noche";
}

/** La hora actual, que se actualiza sola cada minuto (el panel cambia sin recargar la página). */
function useHoraActual(): number | null {
  // La hora es la de la computadora del estudiante: en el servidor no se sabe (null) y se completa al cargar en el navegador.
  return useSyncExternalStore(
    (avisar) => {
      const intervalo = setInterval(avisar, 60_000);
      return () => clearInterval(intervalo);
    },
    () => new Date().getHours(),
    () => null,
  );
}

/** Estrellas, resplandor del sol y la constelación de Astrea; los colores los pone el momento del día (globals.css). */
function DecoracionCielo() {
  return (
    <svg aria-hidden="true" viewBox="0 0 1000 230" preserveAspectRatio="xMaxYMid slice" className="pointer-events-none absolute inset-0 size-full">
      <circle className="cielo-resplandor" cx="900" cy="10" r="110" />
      <g className="cielo-estrellas" fill="#fff">
        <circle cx="70" cy="30" r="1.3" opacity=".7" />
        <circle cx="160" cy="200" r="1" opacity=".5" />
        <circle cx="300" cy="18" r="1.4" opacity=".6" />
        <circle cx="420" cy="205" r="1" opacity=".4" />
        <circle cx="520" cy="40" r="1.2" opacity=".6" />
        <circle cx="380" cy="120" r=".9" opacity=".4" />
        <circle cx="610" cy="215" r="1" opacity=".5" />
        <circle cx="240" cy="110" r=".8" opacity=".5" />
        <circle cx="560" cy="130" r="1" opacity=".4" />
      </g>
      <g fill="none" strokeWidth="1.2">
        <polyline className="cielo-linea" points="700,160 760,90 840,118 880,48 945,82" />
        <line className="cielo-linea" x1="760" y1="90" x2="785" y2="25" />
      </g>
      <g>
        <circle className="cielo-punto" cx="700" cy="160" r="3" />
        <circle className="cielo-punto" cx="760" cy="90" r="3.8" />
        <circle className="cielo-punto" cx="840" cy="118" r="2.8" />
        <circle className="cielo-punto" cx="880" cy="48" r="4.2" />
        <circle className="cielo-punto" cx="945" cy="82" r="2.6" />
        <circle className="cielo-punto" cx="785" cy="25" r="2.8" />
      </g>
    </svg>
  );
}

export default function InicioEstudiante() {
  const { perfil, obtenerToken } = useAuth();
  const [pendientes, setPendientes] = useState<string[]>([]);
  const hora = useHoraActual();

  // Aviso si falta completar el perfil o renovar la validación anual del email o el celular.
  useEffect(() => {
    let vigente = true;
    obtenerToken()
      .then((token) => perfilApi.obtener(token))
      .then((miPerfil) => vigente && setPendientes(pendientesDelPerfil(miPerfil)))
      .catch(() => undefined);
    return () => {
      vigente = false;
    };
  }, [obtenerToken]);
  const nombre = [perfil?.nombre, perfil?.apellido].filter(Boolean).join(" ");
  const rol = tieneRol(perfil, ROLES.administrador) ? "Administración" : tieneRol(perfil, ROLES.docente) ? "Docente" : "Estudiante";

  return (
    <main className="mx-auto flex w-full max-w-5xl flex-1 flex-col gap-6 px-4 py-8 sm:px-6">
      <section
        data-momento={hora === null ? "tarde" : momentoDelDia(hora)}
        className="cielo-astrea relative flex flex-col gap-5 overflow-hidden rounded-2xl border p-6 shadow-md sm:flex-row sm:items-center sm:p-7"
      >
        <DecoracionCielo />
        {/* eslint-disable-next-line @next/next/no-img-element -- SVG estático, no necesita optimización */}
        <img src="/icons/astrea.svg" alt="" width={80} height={80} className="relative size-20 shrink-0 rounded-2xl shadow-md" />
        <div className="relative flex flex-1 flex-col gap-1">
          <p className="text-xs font-bold uppercase tracking-[0.16em] cielo-acento">Portal académico · ISP N.º 20</p>
          <h1 className="text-3xl font-bold sm:text-4xl">
            {hora === null ? "Hola" : saludo(hora)}
            {nombre ? `, ${nombre}` : ""}
          </h1>
          <p className="cielo-texto-2">
            Tu espacio para consultar tu carrera, seguir tus trámites y estar al día con las mesas.
            {!nombre && perfil && <span className="block text-sm">Sesión iniciada como {perfil.email}.</span>}
          </p>
          <p className="font-serif text-sm italic cielo-texto-3">
            Astrea es la diosa griega de la justicia y el orden: precisión en las correlatividades y verificación en cada trámite y en cada
            hallazgo devuelto a su dueño.
          </p>
        </div>
        <span className="cielo-rol relative self-start rounded-full px-3.5 py-1 text-xs font-bold uppercase tracking-wider sm:self-center">
          {rol}
        </span>
      </section>

      <AvisosDeTramites />

      {pendientes.length > 0 && (
        <p role="status" className="flex flex-wrap items-center justify-between gap-3 rounded-xl border border-dorado-500/40 bg-dorado-100/60 px-4 py-3 text-sm text-indigo-950 dark:bg-indigo-900/50 dark:text-indigo-100">
          <span>
            <span className="font-semibold">Completá tu perfil:</span> falta {pendientes.join(", ")}.
          </span>
          <Link href="/mi-perfil" className="rounded-full bg-indigo-700 px-3.5 py-1.5 font-semibold text-white hover:bg-indigo-800">
            Ir a Mi perfil
          </Link>
        </p>
      )}

      <ul className="grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
        {modulos.map((modulo) => (
          <li key={modulo.titulo}>
            <Link
              href={modulo.href}
              className="group flex h-full items-start gap-4 rounded-xl border border-zinc-200 bg-superficie p-4 shadow-sm transition-[translate,box-shadow,border-color] hover:-translate-y-0.5 hover:border-dorado-500/60 hover:shadow-md dark:border-zinc-800"
            >
              <span className="flex size-11 shrink-0 items-center justify-center rounded-lg bg-indigo-50 text-indigo-800 dark:bg-indigo-900/60 dark:text-indigo-200">
                <svg aria-hidden="true" viewBox="0 0 24 24" className="size-5" fill="none" stroke="currentColor" strokeWidth="1.8" strokeLinecap="round" strokeLinejoin="round">
                  {ICONOS[modulo.icono]}
                </svg>
              </span>
              <span className="flex min-w-0 flex-1 flex-col gap-0.5">
                <h2 className="font-sans font-semibold text-indigo-900 dark:text-indigo-100">{modulo.titulo}</h2>
                <span className="text-sm text-zinc-600 dark:text-zinc-400">{modulo.descripcion}</span>
              </span>
              <span aria-hidden="true" className="self-center text-lg text-dorado-500 transition-transform group-hover:translate-x-0.5">
                →
              </span>
            </Link>
          </li>
        ))}
      </ul>

      <section aria-labelledby="sitios-titulo" className="flex flex-col gap-3 rounded-xl border border-zinc-200 bg-superficie p-5 shadow-sm dark:border-zinc-800">
        <div>
          <h2 id="sitios-titulo" className="text-xl font-semibold">
            Páginas del instituto
          </h2>
          <p className="text-sm text-zinc-600 dark:text-zinc-400">Las otras plataformas del ISP N.º 20. Se abren en una pestaña nueva.</p>
        </div>
        <ul className="grid gap-3 sm:grid-cols-2 lg:grid-cols-3">
          {sitiosDelInstituto.map((sitio) => (
            <li key={sitio.href}>
              <a
                href={sitio.href}
                target="_blank"
                rel="noopener noreferrer"
                className="group flex h-full items-center justify-between gap-3 rounded-lg border border-zinc-200 px-4 py-3 transition-colors hover:border-dorado-500/60 hover:bg-dorado-100/40 dark:border-zinc-800 dark:hover:bg-indigo-900/40"
              >
                <span className="flex min-w-0 flex-col">
                  <span className="font-semibold text-indigo-900 dark:text-indigo-100">{sitio.titulo}</span>
                  <span className="text-sm text-zinc-600 dark:text-zinc-400">{sitio.descripcion}</span>
                  <span className="truncate text-xs text-zinc-500">{sitio.href.replace(/^https:\/\//, "").replace(/\/$/, "")}</span>
                </span>
                <span aria-hidden="true" className="text-dorado-500">↗</span>
                <span className="sr-only">(se abre en una pestaña nueva)</span>
              </a>
            </li>
          ))}
        </ul>
      </section>
    </main>
  );
}
