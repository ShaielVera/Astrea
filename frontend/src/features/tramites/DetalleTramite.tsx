import type { ReactNode } from "react";
import { ETIQUETA_ESTADO, fechaHora, type EstadoTramite, type TramiteDetalle } from "./api";

const ESTILO_ESTADO: Record<EstadoTramite, string> = {
  Pendiente: "bg-amber-100 text-amber-900 dark:bg-amber-950 dark:text-amber-200",
  EnProceso: "bg-sky-100 text-sky-900 dark:bg-sky-950 dark:text-sky-200",
  Rechazado: "bg-red-100 text-red-800 dark:bg-red-950 dark:text-red-200",
  Listo: "bg-emerald-100 text-emerald-900 dark:bg-emerald-950 dark:text-emerald-200",
};

export function EstadoBadge({ estado }: { estado: EstadoTramite }) {
  return <span className={`rounded-full px-2 py-0.5 text-xs font-medium ${ESTILO_ESTADO[estado]}`}>{ETIQUETA_ESTADO[estado]}</span>;
}

type Props = {
  tramite: TramiteDetalle;
  /** Muestra datos del titular y del administrador asignado (solo administración). */
  vistaAdministracion?: boolean;
  /** Botones de administración (tomar, aprobar, rechazar). */
  acciones?: ReactNode;
};

/** Detalle de un trámite: estado, datos del formulario, observación de Bedelía e historial. */
export function DetalleTramite({ tramite, vistaAdministracion = false, acciones }: Props) {
  const rechazado = tramite.estado === "Rechazado";

  return (
    <section aria-label={`Trámite ${tramite.numero}`} className="flex flex-col gap-4 rounded-xl border border-zinc-200 p-4 dark:border-zinc-800">
      <div className="flex flex-wrap items-start justify-between gap-2">
        <div className="min-w-0">
          <p className="text-sm text-zinc-500">{tramite.numero}</p>
          <h2 className="text-lg font-semibold">{tramite.tipoNombre}</h2>
          <p className="text-sm text-zinc-500">Pedido el {fechaHora(tramite.fechaSolicitud)}</p>
        </div>
        <EstadoBadge estado={tramite.estado} />
      </div>

      {tramite.observacionAdministrativa && (
        <p
          className={`rounded-md px-3 py-2 text-sm ${
            rechazado ? "bg-red-50 text-red-800 dark:bg-red-950 dark:text-red-200" : "bg-emerald-50 text-emerald-900 dark:bg-emerald-950 dark:text-emerald-200"
          }`}
        >
          <span className="font-medium">{rechazado ? "Motivo del rechazo: " : "Indicación de Bedelía: "}</span>
          {tramite.observacionAdministrativa}
        </p>
      )}

      {vistaAdministracion && (
        <dl className="grid grid-cols-1 gap-x-4 gap-y-1 text-sm sm:grid-cols-[10rem_1fr]">
          <dt className="text-zinc-500">Titular</dt>
          <dd className="min-w-0 break-words">
            {[tramite.solicitante.apellido, tramite.solicitante.nombre].filter(Boolean).join(", ") || "Sin datos cargados"} · {tramite.solicitante.email}
            {tramite.solicitante.legajo ? ` · Legajo ${tramite.solicitante.legajo}` : ""}
          </dd>
          <dt className="text-zinc-500">Asignado a</dt>
          <dd className="min-w-0 break-words">{tramite.administradorAsignado ?? "Nadie todavía"}</dd>
        </dl>
      )}

      <dl className="grid grid-cols-1 gap-x-4 gap-y-1 text-sm sm:grid-cols-[10rem_1fr]">
        {tramite.datos.map((dato) => (
          <div key={dato.clave} className="contents">
            <dt className="text-zinc-500">{dato.etiqueta}</dt>
            <dd className="min-w-0 whitespace-pre-line break-words">{dato.valor}</dd>
          </div>
        ))}
      </dl>

      {acciones}

      <div className="flex flex-col gap-2">
        <h3 className="text-sm font-semibold">Historial</h3>
        <ol aria-label="Historial" className="flex flex-col gap-2 border-l-2 border-zinc-200 pl-4 dark:border-zinc-800">
          {tramite.historial.map((evento, i) => (
            <li key={i} className="flex flex-col text-sm">
              <span>
                <EstadoBadge estado={evento.estadoNuevo} /> <span className="text-zinc-500">{fechaHora(evento.fecha)} · {evento.actor}</span>
              </span>
              {evento.comentario && <span className="mt-1 text-zinc-600 dark:text-zinc-400">{evento.comentario}</span>}
            </li>
          ))}
        </ol>
      </div>
    </section>
  );
}
