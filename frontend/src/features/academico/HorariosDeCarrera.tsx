"use client";

import { useEffect, useState } from "react";
import { useAuth } from "@/features/auth/AuthProvider";
import { mensajeDeError } from "@/features/auth/mensajes-error";
import { ApiError, apiDescargar, apiFetch, apiUrlDeArchivo } from "@/lib/api-client";

/** El PDF de horarios que Secretaría cargó para la carrera (Documentos del asistente → tipo "Horario"). */
type HorarioCarrera = { documentoId: string; titulo: string; anio: number; fechaCarga: string };

type Estado = { tipo: "cargando" } | { tipo: "sin-horario" } | { tipo: "ok"; horario: HorarioCarrera } | { tipo: "error"; mensaje: string };

const botonChico =
  "rounded-md border border-zinc-300 px-3 py-1.5 text-sm font-medium hover:bg-zinc-100 disabled:opacity-60 dark:border-zinc-700 dark:hover:bg-zinc-800";

/**
 * "Ver horarios 2026": días, horas, materias y profesores de cada año, tal cual los publica el instituto.
 * El PDF se muestra en la página; en el celular, donde muchos navegadores no lo muestran adentro, se puede abrir o descargar.
 */
export function HorariosDeCarrera({ carreraId, carrera }: { carreraId: string; carrera: string }) {
  const { obtenerToken } = useAuth();
  const [estado, setEstado] = useState<Estado>({ tipo: "cargando" });
  const [url, setUrl] = useState<string | null>(null);
  const [abriendo, setAbriendo] = useState(false);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    // Se monta de nuevo al cambiar de carrera (key), así que el estado ya arranca limpio.
    let vigente = true;
    obtenerToken()
      .then((token) => apiFetch<HorarioCarrera>(`/carreras/${carreraId}/horarios`, { token }))
      .then((horario) => vigente && setEstado({ tipo: "ok", horario }))
      .catch((e) => {
        if (!vigente) return;
        setEstado(e instanceof ApiError && e.status === 404 ? { tipo: "sin-horario" } : { tipo: "error", mensaje: mensajeDeError(e) });
      });
    return () => {
      vigente = false;
    };
  }, [carreraId, obtenerToken]);

  // La URL local del PDF se libera al cerrarlo o al cambiar de carrera.
  useEffect(() => () => void (url && URL.revokeObjectURL(url)), [url]);

  if (estado.tipo === "cargando") return null;
  if (estado.tipo === "sin-horario") {
    return <p className="text-sm text-zinc-500">Los horarios de esta carrera todavía no están cargados en Astrea.</p>;
  }
  if (estado.tipo === "error") {
    return (
      <p role="alert" className="text-sm text-red-700 dark:text-red-300">
        {estado.mensaje}
      </p>
    );
  }

  const { horario } = estado;

  async function alternar() {
    if (url) {
      setUrl(null);
      return;
    }
    setAbriendo(true);
    setError(null);
    try {
      setUrl(await apiUrlDeArchivo(`/carreras/${carreraId}/horarios/archivo`, await obtenerToken()));
    } catch (e) {
      setError(mensajeDeError(e));
    } finally {
      setAbriendo(false);
    }
  }

  async function descargar() {
    setError(null);
    try {
      await apiDescargar(`/carreras/${carreraId}/horarios/archivo`, await obtenerToken(), `${horario.titulo}.pdf`);
    } catch (e) {
      setError(mensajeDeError(e));
    }
  }

  return (
    <section aria-label={`Horarios ${horario.anio}`} className="flex flex-col gap-3 rounded-xl border border-indigo-200 bg-superficie p-4 dark:border-indigo-900">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <div className="min-w-0">
          <h2 className="font-semibold">Horarios {horario.anio}</h2>
          <p className="text-sm text-zinc-600 dark:text-zinc-400">Días, horas y profesores de cada materia de {carrera}, según el instituto.</p>
        </div>
        <span className="flex flex-wrap gap-2">
          <button type="button" onClick={() => void alternar()} disabled={abriendo} aria-expanded={!!url} className="rounded-md bg-indigo-700 px-4 py-2 text-sm font-semibold text-white hover:bg-indigo-800 disabled:opacity-60">
            {abriendo ? "Abriendo…" : url ? "Ocultar horarios" : `Ver horarios ${horario.anio}`}
          </button>
          <button type="button" onClick={() => void descargar()} className={botonChico}>
            Descargar PDF
          </button>
        </span>
      </div>
      {error && (
        <p role="alert" className="text-sm text-red-700 dark:text-red-300">
          {error}
        </p>
      )}
      {url && (
        <>
          <iframe src={url} title={`Horarios ${horario.anio} de ${carrera}`} className="h-[75vh] w-full rounded-md border border-zinc-200 bg-white dark:border-zinc-800" />
          <a href={url} target="_blank" rel="noopener" className="self-start text-sm text-indigo-700 underline dark:text-indigo-300">
            ¿No se ve? Abrilo en otra pestaña
          </a>
        </>
      )}
    </section>
  );
}
