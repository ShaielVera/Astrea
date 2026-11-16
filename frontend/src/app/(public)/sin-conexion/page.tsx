import type { Metadata } from "next";

export const metadata: Metadata = { title: "Sin conexión · Astrea" };

/**
 * Lo único que el service worker muestra sin red: una página fija, sin datos de nadie.
 * Los datos personales (trámites, conversaciones, perfil) nunca se guardan para verlos sin conexión.
 */
export default function SinConexion() {
  return (
    <main className="mx-auto flex w-full max-w-xl flex-1 flex-col justify-center gap-4 px-4 py-16 sm:px-6">
      <h1 className="text-3xl font-semibold tracking-tight">Sin conexión</h1>
      <p className="text-zinc-700 dark:text-zinc-300">
        No hay conexión a internet. Astrea necesita conexión para mostrar tus datos: por seguridad, no se guardan en el dispositivo.
      </p>
      <p className="text-zinc-700 dark:text-zinc-300">Cuando vuelva la conexión, recargá la página.</p>
    </main>
  );
}
