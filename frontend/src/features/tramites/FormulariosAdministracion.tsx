"use client";

import { useState } from "react";
import type { Persona } from "./api";

const campo = "w-full min-w-0 rounded-md border border-zinc-300 bg-transparent px-3 py-2 font-normal dark:border-zinc-700 dark:bg-zinc-900";
const botonChico =
  "rounded-md border border-zinc-300 px-3 py-1.5 text-sm font-medium hover:bg-zinc-100 disabled:opacity-60 dark:border-zinc-700 dark:hover:bg-zinc-800";
const botonPeligroChico =
  "rounded-md border border-red-300 px-3 py-1.5 text-sm font-medium text-red-700 hover:bg-red-50 disabled:opacity-60 dark:border-red-900 dark:text-red-300 dark:hover:bg-red-950";

type DatosOficiales = { nombre: string; apellido: string; dni: string; legajo: string | null };

/**
 * Datos oficiales del titular (los que se imprimen en el documento). Se piden antes de aprobar
 * un trámite que emite PDF: Bedelía los confirma contra la documentación del estudiante.
 */
export function FormularioDatosOficiales({ persona, onGuardar }: { persona: Persona; onGuardar: (datos: DatosOficiales) => Promise<void> }) {
  const [nombre, setNombre] = useState(persona.nombre ?? "");
  const [apellido, setApellido] = useState(persona.apellido ?? "");
  const [dni, setDni] = useState(persona.dni ?? "");
  const [legajo, setLegajo] = useState(persona.legajo ?? "");
  const [guardando, setGuardando] = useState(false);
  const completos = Boolean(persona.nombre && persona.apellido && persona.dni);

  return (
    <form
      aria-label="Datos oficiales del titular"
      className="flex flex-col gap-2"
      onSubmit={async (e) => {
        e.preventDefault();
        setGuardando(true);
        try {
          await onGuardar({ nombre, apellido, dni, legajo: legajo.trim() || null });
        } finally {
          setGuardando(false);
        }
      }}
    >
      <p className="text-sm font-medium">
        Datos oficiales del titular{" "}
        <span className={completos ? "text-emerald-700 dark:text-emerald-300" : "text-amber-700 dark:text-amber-300"}>
          {completos ? "(completos)" : "(faltan: se imprimen en el documento)"}
        </span>
      </p>
      <div className="grid grid-cols-1 gap-2 sm:grid-cols-2">
        <label className="flex min-w-0 flex-col gap-1 text-sm">
          Nombre
          <input value={nombre} onChange={(e) => setNombre(e.target.value)} required maxLength={100} className={campo} />
        </label>
        <label className="flex min-w-0 flex-col gap-1 text-sm">
          Apellido
          <input value={apellido} onChange={(e) => setApellido(e.target.value)} required maxLength={100} className={campo} />
        </label>
        <label className="flex min-w-0 flex-col gap-1 text-sm">
          DNI
          <input value={dni} onChange={(e) => setDni(e.target.value)} required inputMode="numeric" maxLength={12} className={campo} />
        </label>
        <label className="flex min-w-0 flex-col gap-1 text-sm">
          Legajo (opcional)
          <input value={legajo} onChange={(e) => setLegajo(e.target.value)} maxLength={30} className={campo} />
        </label>
      </div>
      <div>
        <button type="submit" className={botonChico} disabled={guardando}>
          Guardar datos oficiales
        </button>
      </div>
    </form>
  );
}

/** Anular un documento emitido por error: deja de descargarse y la validación pública lo muestra anulado. */
export function FormularioAnulacion({ onAnular }: { onAnular: (motivo: string) => Promise<void> }) {
  const [abierto, setAbierto] = useState(false);
  const [motivo, setMotivo] = useState("");

  if (!abierto) {
    return (
      <button type="button" className={botonPeligroChico} onClick={() => setAbierto(true)}>
        Anular documento
      </button>
    );
  }

  return (
    <form
      aria-label="Anular documento"
      className="flex w-full flex-col gap-2"
      onSubmit={(e) => {
        e.preventDefault();
        void onAnular(motivo);
      }}
    >
      <label className="flex min-w-0 flex-col gap-1 text-sm font-medium">
        Motivo de la anulación
        <input value={motivo} onChange={(e) => setMotivo(e.target.value)} required maxLength={300} className={campo} />
      </label>
      <div className="flex gap-2">
        <button type="submit" className={botonPeligroChico}>
          Confirmar anulación
        </button>
        <button type="button" className={botonChico} onClick={() => setAbierto(false)}>
          Volver
        </button>
      </div>
    </form>
  );
}
