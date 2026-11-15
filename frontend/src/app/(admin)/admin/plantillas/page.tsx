"use client";

import Link from "next/link";
import { useEffect, useState } from "react";
import { useAuth } from "@/features/auth/AuthProvider";
import { mensajeDeError } from "@/features/auth/mensajes-error";
import { tramitesApi, type PlantillaDeTipo } from "@/features/tramites/api";

const campo = "w-full min-w-0 rounded-md border border-zinc-300 bg-transparent px-3 py-2 font-normal dark:border-zinc-700 dark:bg-zinc-900";
const botonPrimario = "rounded-md bg-indigo-600 px-4 py-2 text-sm font-medium text-white hover:bg-indigo-700 disabled:opacity-60";

export default function PlantillasPage() {
  const { obtenerToken } = useAuth();
  const [plantillas, setPlantillas] = useState<PlantillaDeTipo[] | null>(null);
  const [aviso, setAviso] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    let vigente = true;
    obtenerToken()
      .then((token) => tramitesApi.plantillas(token))
      .then((lista) => vigente && setPlantillas(lista))
      .catch((e) => vigente && setError(mensajeDeError(e)));
    return () => {
      vigente = false;
    };
  }, [obtenerToken]);

  async function guardar(tipo: PlantillaDeTipo, titulo: string, cuerpo: string) {
    setError(null);
    setAviso(null);
    try {
      const token = await obtenerToken();
      await tramitesApi.nuevaVersionPlantilla(token, tipo.tipoId, { titulo, cuerpo });
      setPlantillas(await tramitesApi.plantillas(token));
      setAviso(`Se guardó una versión nueva de "${tipo.tipoNombre}". Los documentos ya emitidos conservan la suya.`);
    } catch (e) {
      setError(mensajeDeError(e));
    }
  }

  return (
    <main className="mx-auto flex w-full max-w-5xl flex-1 flex-col gap-6 px-4 py-10 sm:px-6">
      <div className="flex flex-col gap-1">
        <Link href="/admin" className="text-sm text-indigo-600 hover:underline dark:text-indigo-400">
          ← Administración
        </Link>
        <h1 className="text-3xl font-semibold tracking-tight">Plantillas de documentos</h1>
        <p className="text-zinc-600 dark:text-zinc-400">
          El texto de cada constancia. Corregirlo crea una versión nueva: los documentos ya emitidos no cambian y cada uno indica con qué versión se generó.
        </p>
      </div>

      {aviso && (
        <p role="status" className="rounded-md bg-emerald-50 px-3 py-2 text-sm text-emerald-800 dark:bg-emerald-950 dark:text-emerald-200">
          {aviso}
        </p>
      )}
      {error && (
        <p role="alert" className="rounded-md bg-red-50 px-3 py-2 text-sm text-red-700 dark:bg-red-950 dark:text-red-300">
          {error}
        </p>
      )}

      {plantillas?.map((tipo) => (
        <EditorPlantilla key={`${tipo.tipoId}-${tipo.vigente?.version ?? 0}`} tipo={tipo} onGuardar={(titulo, cuerpo) => guardar(tipo, titulo, cuerpo)} />
      ))}
    </main>
  );
}

function EditorPlantilla({ tipo, onGuardar }: { tipo: PlantillaDeTipo; onGuardar: (titulo: string, cuerpo: string) => Promise<void> }) {
  const [titulo, setTitulo] = useState(tipo.vigente?.titulo ?? tipo.tipoNombre);
  const [cuerpo, setCuerpo] = useState(tipo.vigente?.cuerpo ?? "");
  const [guardando, setGuardando] = useState(false);

  return (
    <form
      aria-label={`Plantilla de ${tipo.tipoNombre}`}
      className="flex flex-col gap-3 rounded-xl border border-zinc-200 p-4 dark:border-zinc-800"
      onSubmit={async (e) => {
        e.preventDefault();
        setGuardando(true);
        try {
          await onGuardar(titulo, cuerpo);
        } finally {
          setGuardando(false);
        }
      }}
    >
      <div className="flex flex-wrap items-baseline justify-between gap-2">
        <h2 className="text-lg font-semibold">{tipo.tipoNombre}</h2>
        <span className="text-sm text-zinc-500">
          {tipo.vigente ? `Versión ${tipo.vigente.version} vigente` : "No emite PDF: se resuelve en Bedelía"}
        </span>
      </div>
      <label className="flex min-w-0 flex-col gap-1 text-sm font-medium">
        Título del documento
        <input value={titulo} onChange={(e) => setTitulo(e.target.value)} required maxLength={120} className={campo} />
      </label>
      <label className="flex min-w-0 flex-col gap-1 text-sm font-medium">
        Texto
        <textarea
          value={cuerpo}
          onChange={(e) => setCuerpo(e.target.value)}
          required
          rows={6}
          maxLength={4000}
          aria-describedby={`marcadores-${tipo.tipoId}`}
          className={`${campo} font-mono text-sm`}
        />
      </label>
      <p id={`marcadores-${tipo.tipoId}`} className="text-xs text-zinc-500">
        Datos disponibles: {tipo.marcadoresDisponibles.map((clave) => `{{${clave}}}`).join(" ")}. Para un dato opcional podés indicar un texto por
        defecto: {"{{presentarAnte|quien corresponda}}"}. Dejá una línea en blanco para separar párrafos.
      </p>
      <div>
        <button type="submit" className={botonPrimario} disabled={guardando}>
          {tipo.vigente ? "Guardar como versión nueva" : "Crear plantilla"}
        </button>
      </div>
    </form>
  );
}
