import type { ReactNode } from "react";
import { NOMBRE_ANIO, type CondicionRequisito, type CorrelatividadesDeMateria, type Requisito } from "./api";

const ESTILO_CONDICION: Record<CondicionRequisito, string> = {
  Regularizada: "bg-amber-100 text-amber-900 dark:bg-amber-950 dark:text-amber-200",
  Aprobada: "bg-emerald-100 text-emerald-900 dark:bg-emerald-950 dark:text-emerald-200",
};

type Acciones = {
  /** Al tocar una materia requisito (por ejemplo, para ver sus propias correlatividades). */
  onElegirMateria?: (materiaId: string) => void;
  /** Contenido extra por requisito (por ejemplo, el botón "Quitar" del panel de administración). */
  acciones?: (requisito: Requisito) => ReactNode;
};

/** Requisitos para cursar y para rendir, en dos bloques diferenciados. */
export function RequisitosCorrelatividad({ detalle, ...acciones }: { detalle: CorrelatividadesDeMateria } & Acciones) {
  return (
    <div className="grid gap-4 md:grid-cols-2">
      <BloqueRequisitos
        titulo="Para cursar"
        explicacion="Lo que tenés que tener para inscribirte a cursar."
        requisitos={detalle.paraCursar}
        {...acciones}
      />
      <BloqueRequisitos
        titulo="Para rendir el final"
        explicacion="Lo que tenés que tener para inscribirte a la mesa de examen."
        requisitos={detalle.paraRendir}
        {...acciones}
      />
    </div>
  );
}

function BloqueRequisitos({ titulo, explicacion, requisitos, ...acciones }: { titulo: string; explicacion: string; requisitos: Requisito[] } & Acciones) {
  const obligatorios = requisitos.filter((r) => r.grupoAlternativa === null);
  const grupos = new Map<number, Requisito[]>();
  for (const requisito of requisitos) {
    if (requisito.grupoAlternativa !== null) {
      grupos.set(requisito.grupoAlternativa, [...(grupos.get(requisito.grupoAlternativa) ?? []), requisito]);
    }
  }

  return (
    <section aria-label={titulo} className="flex flex-col gap-3 rounded-xl border border-zinc-200 p-4 dark:border-zinc-800">
      <div>
        <h3 className="font-semibold">{titulo}</h3>
        <p className="text-xs text-zinc-500">{explicacion}</p>
      </div>

      {requisitos.length === 0 ? (
        <p className="text-sm text-zinc-600 dark:text-zinc-400">No tiene correlatividades.</p>
      ) : (
        <>
          {obligatorios.length > 0 && (
            <ul className="flex flex-col gap-2">
              {obligatorios.map((requisito) => (
                <ItemRequisito key={requisito.id} requisito={requisito} {...acciones} />
              ))}
            </ul>
          )}
          {[...grupos.entries()].map(([grupo, alternativas]) => (
            <div key={grupo} className="flex flex-col gap-2 rounded-lg border border-dashed border-zinc-300 p-3 dark:border-zinc-700">
              <p className="text-xs font-medium text-zinc-600 dark:text-zinc-400">Al menos una de estas:</p>
              <ul className="flex flex-col gap-2">
                {alternativas.map((requisito) => (
                  <ItemRequisito key={requisito.id} requisito={requisito} {...acciones} />
                ))}
              </ul>
            </div>
          ))}
        </>
      )}
    </section>
  );
}

function ItemRequisito({ requisito, onElegirMateria, acciones }: { requisito: Requisito } & Acciones) {
  return (
    <li className="flex flex-wrap items-center justify-between gap-2 text-sm">
      <div className="flex min-w-0 flex-col">
        {requisito.materiaId === null ? (
          <>
            <span>{requisito.descripcion}</span>
            <span className="text-xs text-zinc-500">Lo verifica Bedelía</span>
          </>
        ) : onElegirMateria ? (
          <button
            type="button"
            onClick={() => onElegirMateria(requisito.materiaId!)}
            className="text-left font-medium text-indigo-700 underline-offset-2 hover:underline dark:text-indigo-300"
          >
            {requisito.nombre}
          </button>
        ) : (
          <span className="font-medium">{requisito.nombre}</span>
        )}
        {requisito.anio !== null && <span className="text-xs text-zinc-500">{NOMBRE_ANIO[requisito.anio] ?? `Año ${requisito.anio}`}</span>}
      </div>
      <div className="flex items-center gap-2">
        <span className={`rounded-full px-2 py-0.5 text-xs font-medium ${ESTILO_CONDICION[requisito.condicion]}`}>{requisito.condicion}</span>
        {acciones?.(requisito)}
      </div>
    </li>
  );
}
