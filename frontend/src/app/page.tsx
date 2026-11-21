import Link from "next/link";
import { ICONOS, MODULOS } from "@/features/inicio/modulos";
import { IrAlInicioConSesion } from "@/features/inicio/IrAlInicioConSesion";

/** Portada pública: qué es Astrea y sus módulos. Con la sesión iniciada, lleva directo al inicio. */
export default function Portada() {
  return (
    <main className="mx-auto flex w-full max-w-5xl flex-1 flex-col gap-10 px-4 py-12 sm:px-6">
      <IrAlInicioConSesion />
      <header className="flex flex-col items-start gap-6 rounded-2xl border border-dorado-500/30 bg-gradient-to-br from-dorado-100/60 to-superficie p-6 sm:flex-row sm:items-center sm:p-8 dark:from-indigo-900/60 dark:to-indigo-950">
        {/* eslint-disable-next-line @next/next/no-img-element -- SVG estático, no necesita optimización */}
        <img src="/icons/astrea.svg" alt="" width={112} height={112} className="size-24 shrink-0 drop-shadow-md sm:size-28" />
        <div className="flex flex-1 flex-col gap-2">
          <p className="text-xs font-semibold uppercase tracking-[0.18em] text-dorado-700 dark:text-dorado-300">
            Instituto Superior de Profesorado N.º 20
          </p>
          <h1 className="text-4xl font-semibold tracking-tight sm:text-5xl">Astrea</h1>
          <p className="max-w-2xl text-lg text-zinc-600 dark:text-zinc-400">
            El portal académico del instituto: tu carrera, tus mesas y tus trámites en un solo lugar, y un asistente que responde con
            los reglamentos oficiales.
          </p>
          <p className="max-w-2xl font-serif text-sm text-zinc-500 italic dark:text-zinc-400">
            Astrea es la diosa griega de la justicia y el orden: precisión en las correlatividades y verificación en cada trámite y en
            cada hallazgo devuelto a su dueño.
          </p>
          <Link
            href="/login"
            className="mt-2 self-start rounded-full bg-indigo-700 px-5 py-2 font-semibold text-white hover:bg-indigo-800"
          >
            Ingresar
          </Link>
        </div>
      </header>

      <section aria-labelledby="modulos-titulo" className="flex flex-col gap-4">
        <h2 id="modulos-titulo" className="text-xl font-semibold">
          Módulos
        </h2>
        <ul className="grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
          {MODULOS.map((modulo) => (
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
                  <h3 className="font-sans font-semibold text-indigo-900 dark:text-indigo-100">{modulo.titulo}</h3>
                  <span className="text-sm text-zinc-600 dark:text-zinc-400">{modulo.descripcion}</span>
                </span>
              </Link>
            </li>
          ))}
        </ul>
      </section>
    </main>
  );
}
