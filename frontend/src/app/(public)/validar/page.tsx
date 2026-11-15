"use client";

import { useRouter } from "next/navigation";
import { useState, type FormEvent } from "react";

/** Para quien recibe un documento impreso y prefiere tipear el código en lugar de escanear el QR. */
export default function IngresarCodigoPage() {
  const router = useRouter();
  const [codigo, setCodigo] = useState("");

  function validar(evento: FormEvent<HTMLFormElement>) {
    evento.preventDefault();
    router.push(`/validar/${encodeURIComponent(codigo.trim().toUpperCase())}`);
  }

  return (
    <main className="mx-auto flex w-full max-w-2xl flex-1 flex-col gap-6 px-4 py-10 sm:px-6">
      <div className="flex flex-col gap-1">
        <h1 className="text-3xl font-semibold tracking-tight">Validación de documentos</h1>
        <p className="text-zinc-600 dark:text-zinc-400">
          Ingresá el código de validación que figura al pie del documento (por ejemplo, 7K3M-Q9TB-2HXA).
        </p>
      </div>
      <form onSubmit={validar} aria-label="Validar un documento" className="flex flex-col gap-3 sm:flex-row sm:items-end">
        <label className="flex min-w-0 flex-1 flex-col gap-1 text-sm font-medium">
          Código de validación
          <input
            value={codigo}
            onChange={(e) => setCodigo(e.target.value)}
            required
            minLength={12}
            maxLength={16}
            autoComplete="off"
            className="w-full min-w-0 rounded-md border border-zinc-300 bg-transparent px-3 py-2 font-mono uppercase dark:border-zinc-700 dark:bg-zinc-900"
          />
        </label>
        <button type="submit" className="rounded-md bg-indigo-600 px-4 py-2 font-medium text-white hover:bg-indigo-700">
          Validar
        </button>
      </form>
    </main>
  );
}
