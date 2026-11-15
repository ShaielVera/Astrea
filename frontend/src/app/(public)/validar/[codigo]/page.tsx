import { Suspense } from "react";
import { ResultadoValidacion } from "@/features/tramites/ResultadoValidacion";

/** Destino del QR impreso en los documentos: cualquiera puede verificarlo, sin iniciar sesión. */
export default function ValidarDocumentoPage() {
  return (
    <main className="mx-auto flex w-full max-w-2xl flex-1 flex-col gap-6 px-4 py-10 sm:px-6">
      <div className="flex flex-col gap-1">
        <h1 className="text-3xl font-semibold tracking-tight">Validación de documentos</h1>
        <p className="text-zinc-600 dark:text-zinc-400">Comprobá si un documento fue emitido por el instituto a través de Astrea.</p>
      </div>
      <Suspense fallback={<p className="text-zinc-500">Verificando el documento…</p>}>
        <ResultadoValidacion />
      </Suspense>
    </main>
  );
}
