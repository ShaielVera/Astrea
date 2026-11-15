"use client";

import { useEffect, useState, type ReactNode } from "react";
import { useAuth } from "@/features/auth/AuthProvider";
import { CATEGORIAS, ESTADOS, FRANJAS, PUNTOS, fechaCorta, hallazgosApi, type EstadoObjeto, type HallazgoPublico } from "./api";

/**
 * Foto de un hallazgo. Se pide con el token (no es una URL pública) y se muestra desde memoria.
 * Usa object-cover dentro de un recuadro fijo para no deformar la imagen.
 */
export function FotoHallazgo({ id, titulo }: { id: string; titulo: string }) {
  const { obtenerToken } = useAuth();
  const [url, setUrl] = useState<string | null>(null);

  useEffect(() => {
    let vigente = true;
    let creada: string | null = null;
    obtenerToken()
      .then((token) => hallazgosApi.foto(token, id))
      .then((blob) => {
        if (!vigente || !blob) return;
        creada = URL.createObjectURL(blob);
        setUrl(creada);
      })
      .catch(() => undefined);
    return () => {
      vigente = false;
      if (creada) URL.revokeObjectURL(creada);
    };
  }, [id, obtenerToken]);

  return (
    <div className="aspect-[4/3] w-full overflow-hidden rounded-lg bg-zinc-100 dark:bg-zinc-800">
      {url && (
        // eslint-disable-next-line @next/next/no-img-element -- la imagen viene de un blob protegido, no de una URL que Next pueda optimizar.
        <img src={url} alt={`Foto de ${titulo}`} className="h-full w-full object-cover" />
      )}
    </div>
  );
}

const ESTILO_ESTADO: Record<EstadoObjeto, string> = {
  Publicado: "bg-indigo-100 text-indigo-900 dark:bg-indigo-950 dark:text-indigo-200",
  Entregado: "bg-emerald-100 text-emerald-900 dark:bg-emerald-950 dark:text-emerald-200",
  DadoDeBaja: "bg-zinc-200 text-zinc-700 dark:bg-zinc-800 dark:text-zinc-300",
};

export function EstadoHallazgo({ estado }: { estado: EstadoObjeto }) {
  return <span className={`rounded-full px-2 py-0.5 text-xs font-medium ${ESTILO_ESTADO[estado]}`}>{ESTADOS[estado]}</span>;
}

/** Tarjeta con los datos públicos de un hallazgo. `extra` agrega contenido (estado, datos privados, acciones). */
export function TarjetaHallazgo({ hallazgo, extra }: { hallazgo: HallazgoPublico; extra?: ReactNode }) {
  return (
    <article aria-label={hallazgo.tituloPublico} className="flex min-w-0 flex-col gap-3 rounded-xl border border-zinc-200 p-4 dark:border-zinc-800">
      {hallazgo.tieneFoto && <FotoHallazgo id={hallazgo.id} titulo={hallazgo.tituloPublico} />}
      <div className="flex flex-col gap-1">
        <h3 className="font-semibold">{hallazgo.tituloPublico}</h3>
        <p className="text-xs text-zinc-500">{CATEGORIAS[hallazgo.categoria].split(" (")[0]}</p>
      </div>
      <dl className="grid grid-cols-[auto_1fr] gap-x-3 gap-y-1 text-sm">
        <dt className="text-zinc-500">Encontrado</dt>
        <dd className="min-w-0 break-words">
          {hallazgo.sectorEncontrado} · {fechaCorta(hallazgo.fechaHallazgo)} ({FRANJAS[hallazgo.franjaHoraria].toLowerCase()})
        </dd>
        <dt className="text-zinc-500">Retiralo en</dt>
        <dd className="font-medium">{PUNTOS[hallazgo.puntoResguardo]}</dd>
      </dl>
      {extra}
    </article>
  );
}
