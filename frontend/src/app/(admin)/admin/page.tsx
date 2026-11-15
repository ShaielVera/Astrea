"use client";

import Link from "next/link";
import { useEffect, useState } from "react";
import { useAuth } from "@/features/auth/AuthProvider";
import { mensajeDeError } from "@/features/auth/mensajes-error";
import { apiFetch } from "@/lib/api-client";

type ResumenUsuarios = { estudiantes: number; administradores: number };

type Estado = { tipo: "cargando" } | { tipo: "ok"; resumen: ResumenUsuarios } | { tipo: "error"; mensaje: string };

export default function PanelAdministracion() {
  const { obtenerToken } = useAuth();
  const [estado, setEstado] = useState<Estado>({ tipo: "cargando" });

  useEffect(() => {
    let vigente = true;
    (async () => {
      try {
        const resumen = await apiFetch<ResumenUsuarios>("/admin/usuarios/resumen", { token: await obtenerToken() });
        if (vigente) setEstado({ tipo: "ok", resumen });
      } catch (error) {
        if (vigente) setEstado({ tipo: "error", mensaje: mensajeDeError(error) });
      }
    })();
    return () => {
      vigente = false;
    };
  }, [obtenerToken]);

  return (
    <main className="mx-auto flex w-full max-w-5xl flex-1 flex-col gap-8 px-4 py-10 sm:px-6">
      <div className="flex flex-col gap-1">
        <h1 className="text-3xl font-semibold tracking-tight">Administración</h1>
        <p className="text-zinc-600 dark:text-zinc-400">Panel de Bedelía / Secretaría.</p>
      </div>

      <nav aria-label="Secciones de administración" className="flex flex-wrap gap-3">
        <Link
          href="/admin/academico"
          className="rounded-xl border border-zinc-200 px-5 py-4 font-medium hover:border-indigo-400 dark:border-zinc-800"
        >
          Plan de estudios
          <span className="block text-sm font-normal text-zinc-500">Carreras, planes y materias</span>
        </Link>
        <Link
          href="/admin/mesas"
          className="rounded-xl border border-zinc-200 px-5 py-4 font-medium hover:border-indigo-400 dark:border-zinc-800"
        >
          Mesas de examen
          <span className="block text-sm font-normal text-zinc-500">Llamados, tribunales y publicación</span>
        </Link>
        <Link
          href="/admin/tramites"
          className="rounded-xl border border-zinc-200 px-5 py-4 font-medium hover:border-indigo-400 dark:border-zinc-800"
        >
          Trámites
          <span className="block text-sm font-normal text-zinc-500">Bandeja de pedidos de estudiantes</span>
        </Link>
      </nav>

      <section aria-labelledby="usuarios-titulo" className="flex flex-col gap-3">
        <h2 id="usuarios-titulo" className="text-lg font-semibold">
          Usuarios activos
        </h2>
        {estado.tipo === "cargando" && (
          <p role="status" className="text-zinc-500">
            Cargando…
          </p>
        )}
        {estado.tipo === "error" && (
          <p role="alert" className="text-red-700 dark:text-red-300">
            {estado.mensaje}
          </p>
        )}
        {estado.tipo === "ok" && (
          <dl className="grid max-w-md grid-cols-2 gap-4">
            <div className="rounded-xl border border-zinc-200 p-4 dark:border-zinc-800">
              <dt className="text-sm text-zinc-500">Estudiantes</dt>
              <dd className="text-3xl font-semibold" data-testid="total-estudiantes">
                {estado.resumen.estudiantes}
              </dd>
            </div>
            <div className="rounded-xl border border-zinc-200 p-4 dark:border-zinc-800">
              <dt className="text-sm text-zinc-500">Administradores</dt>
              <dd className="text-3xl font-semibold" data-testid="total-administradores">
                {estado.resumen.administradores}
              </dd>
            </div>
          </dl>
        )}
      </section>
    </main>
  );
}
