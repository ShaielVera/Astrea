import type { ReactNode } from "react";
import { agruparPorDia, fechaCorta, fechaLarga, horaCorta, type EstadoInscripcion, type MesaExamen } from "./api";

const INSCRIPCION: Record<EstadoInscripcion, { texto: string; estilo: string } | null> = {
  SinFechas: null,
  Proxima: { texto: "Inscripción próxima", estilo: "bg-sky-100 text-sky-900 dark:bg-sky-950 dark:text-sky-200" },
  Abierta: { texto: "Inscripción abierta", estilo: "bg-emerald-100 text-emerald-900 dark:bg-emerald-950 dark:text-emerald-200" },
  Cerrada: { texto: "Inscripción cerrada", estilo: "bg-zinc-100 text-zinc-700 dark:bg-zinc-800 dark:text-zinc-300" },
};

const ESTADO_ADMIN = {
  Borrador: "bg-amber-100 text-amber-900 dark:bg-amber-950 dark:text-amber-200",
  Publicada: "bg-indigo-100 text-indigo-900 dark:bg-indigo-950 dark:text-indigo-200",
  Cancelada: "bg-red-100 text-red-800 dark:bg-red-950 dark:text-red-200",
} as const;

type Props = {
  mesas: MesaExamen[];
  /** Muestra el estado (borrador, publicada…) para el panel de administración. */
  mostrarEstado?: boolean;
  /** Contenido extra por mesa (botones de administración). */
  acciones?: (mesa: MesaExamen) => ReactNode;
};

/** Listado tipo calendario: un bloque por día, con las mesas en orden de hora. */
export function ListadoMesas({ mesas, mostrarEstado = false, acciones }: Props) {
  return (
    <div className="flex flex-col gap-6">
      {agruparPorDia(mesas).map(([fecha, delDia]) => (
        <section key={fecha} aria-label={fechaLarga(fecha)} className="flex flex-col gap-2">
          <h2 className="text-sm font-semibold uppercase tracking-wide text-zinc-500 first-letter:uppercase">{fechaLarga(fecha)}</h2>
          <ul className="flex flex-col gap-3">
            {delDia.map((mesa) => (
              <ItemMesa key={mesa.id} mesa={mesa} mostrarEstado={mostrarEstado} acciones={acciones} />
            ))}
          </ul>
        </section>
      ))}
    </div>
  );
}

function ItemMesa({ mesa, mostrarEstado, acciones }: { mesa: MesaExamen } & Omit<Props, "mesas">) {
  const cancelada = mesa.estado === "Cancelada";
  const inscripcion = INSCRIPCION[mesa.estadoInscripcion];

  return (
    <li
      aria-label={`${mesa.materiaNombre}, ${horaCorta(mesa.hora)}`}
      className={`flex flex-col gap-3 rounded-xl border p-4 sm:flex-row sm:items-start ${
        cancelada ? "border-red-200 bg-red-50/40 dark:border-red-900 dark:bg-red-950/20" : "border-zinc-200 dark:border-zinc-800"
      }`}
    >
      <div className="w-16 shrink-0 text-2xl font-semibold tabular-nums">
        <span className={cancelada ? "line-through decoration-2" : undefined}>{horaCorta(mesa.hora)}</span>
      </div>

      <div className="flex min-w-0 flex-1 flex-col gap-1">
        <div className="flex flex-wrap items-center gap-2">
          <h3 className="font-semibold">{mesa.materiaNombre}</h3>
          {mostrarEstado && (
            <span className={`rounded-full px-2 py-0.5 text-xs font-medium ${ESTADO_ADMIN[mesa.estado]}`}>{mesa.estado}</span>
          )}
          {cancelada && !mostrarEstado && (
            <span className="rounded-full bg-red-100 px-2 py-0.5 text-xs font-medium text-red-800 dark:bg-red-950 dark:text-red-200">Cancelada</span>
          )}
        </div>
        <p className="text-sm text-zinc-600 dark:text-zinc-400">
          {mesa.carreraNombre} · {mesa.turno}, {mesa.llamado}.º llamado{mesa.aula ? ` · ${mesa.aula}` : ""}
        </p>
        <p className="text-sm">
          <span className="text-zinc-500">Tribunal: </span>
          {mesa.tribunal.join(" · ")}
        </p>
        {cancelada && mesa.motivoCancelacion && (
          <p className="text-sm font-medium text-red-800 dark:text-red-300">Motivo: {mesa.motivoCancelacion}</p>
        )}
        {mesa.observaciones && <p className="text-sm text-zinc-600 dark:text-zinc-400">{mesa.observaciones}</p>}
        {inscripcion && mesa.inscripcionDesde && mesa.inscripcionHasta && (
          <p className="flex flex-wrap items-center gap-2 text-sm">
            <span className={`rounded-full px-2 py-0.5 text-xs font-medium ${inscripcion.estilo}`}>{inscripcion.texto}</span>
            <span className="text-zinc-500">
              del {fechaCorta(mesa.inscripcionDesde)} al {fechaCorta(mesa.inscripcionHasta)}
            </span>
          </p>
        )}
      </div>

      {acciones && <div className="flex flex-wrap items-start gap-2">{acciones(mesa)}</div>}
    </li>
  );
}
