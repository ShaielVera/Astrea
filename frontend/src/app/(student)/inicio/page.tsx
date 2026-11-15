"use client";

import Link from "next/link";
import { useAuth } from "@/features/auth/AuthProvider";

const modulos: { titulo: string; descripcion: string; href?: string }[] = [
  { titulo: "Plan de estudios", descripcion: "Las materias de cada carrera, año por año.", href: "/plan-de-estudios" },
  { titulo: "Correlatividades", descripcion: "Qué necesitás para cursar y para rendir cada materia.", href: "/correlatividades" },
  { titulo: "Mesas de examen", descripcion: "Llamados, fechas, horarios y tribunales.", href: "/mesas" },
  { titulo: "Mis trámites", descripcion: "Pedí constancias y seguí su estado.", href: "/tramites" },
  { titulo: "Hallazgos", descripcion: "Objetos perdidos encontrados en el instituto." },
  { titulo: "Asistente", descripcion: "Consultá en lenguaje natural." },
];

export default function InicioEstudiante() {
  const { perfil } = useAuth();

  return (
    <main className="mx-auto flex w-full max-w-5xl flex-1 flex-col gap-8 px-4 py-10 sm:px-6">
      <div className="flex flex-col gap-1">
        <h1 className="text-3xl font-semibold tracking-tight">Mi inicio</h1>
        <p className="text-zinc-600 dark:text-zinc-400">Sesión iniciada como {perfil?.email}.</p>
      </div>

      <ul className="grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
        {modulos.map((modulo) => (
          <li key={modulo.titulo} className="flex flex-col gap-2 rounded-xl border border-zinc-200 p-5 dark:border-zinc-800">
            <div className="flex items-center justify-between gap-2">
              <h2 className="font-medium">
                {modulo.href ? (
                  <Link href={modulo.href} className="text-indigo-600 hover:underline dark:text-indigo-400">
                    {modulo.titulo}
                  </Link>
                ) : (
                  modulo.titulo
                )}
              </h2>
              {!modulo.href && (
                <span className="rounded-full bg-zinc-100 px-2 py-0.5 text-xs text-zinc-600 dark:bg-zinc-800 dark:text-zinc-300">
                  Próximamente
                </span>
              )}
            </div>
            <p className="text-sm text-zinc-600 dark:text-zinc-400">{modulo.descripcion}</p>
          </li>
        ))}
      </ul>
    </main>
  );
}
