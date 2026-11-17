import type { ReactNode } from "react";
import { agruparPorAnio, ETIQUETA_CUATRIMESTRE, NOMBRE_ANIO, type MateriaDePlan } from "./api";

type Props = {
  materias: MateriaDePlan[];
  /** Contenido extra por materia (por ejemplo, el botón "Editar" del panel de administración). */
  acciones?: (materia: MateriaDePlan) => ReactNode;
};

export function ListaMateriasPorAnio({ materias, acciones }: Props) {
  if (materias.length === 0) {
    return <p className="text-zinc-500">Este plan todavía no tiene materias cargadas.</p>;
  }

  return (
    <div className="flex flex-col gap-6">
      {agruparPorAnio(materias).map(([anio, delAnio]) => (
        <section key={anio} aria-labelledby={`anio-${anio}`} className="flex flex-col gap-2">
          <h3 id={`anio-${anio}`} className="text-lg font-semibold">
            {NOMBRE_ANIO[anio] ?? `Año ${anio}`}
          </h3>
          <ul className="divide-y divide-zinc-200 rounded-xl border bg-superficie border-zinc-200 dark:divide-zinc-800 dark:border-zinc-800">
            {delAnio.map((materia) => (
              <li key={materia.planMateriaId} className="flex flex-wrap items-center justify-between gap-2 px-4 py-3">
                <div className="flex min-w-0 flex-col">
                  <span className={materia.activa ? "font-medium" : "font-medium text-zinc-400 line-through"}>{materia.nombre}</span>
                  <span className="text-xs text-zinc-500">{materia.codigo}</span>
                </div>
                <div className="flex flex-wrap items-center gap-2">
                  <span className="rounded-full bg-zinc-100 px-2 py-0.5 text-xs text-zinc-600 dark:bg-zinc-800 dark:text-zinc-300">
                    {ETIQUETA_CUATRIMESTRE[materia.cuatrimestre]}
                  </span>
                  {materia.cargaHorariaSemanal && (
                    <span className="text-xs text-zinc-500">{materia.cargaHorariaSemanal} h/sem</span>
                  )}
                  {!materia.activa && <span className="text-xs font-medium text-amber-700 dark:text-amber-400">Inactiva</span>}
                  {acciones?.(materia)}
                </div>
              </li>
            ))}
          </ul>
        </section>
      ))}
    </div>
  );
}
