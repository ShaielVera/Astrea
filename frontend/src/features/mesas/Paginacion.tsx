import type { Pagina } from "@/features/academico/api";

const boton =
  "rounded-md border border-zinc-300 px-3 py-1.5 text-sm font-medium hover:bg-zinc-100 disabled:opacity-50 dark:border-zinc-700 dark:hover:bg-zinc-800";

/** Controles "Anterior / Siguiente" para un resultado paginado de la API. */
export function Paginacion<T>({ pagina, onCambiar }: { pagina: Pagina<T>; onCambiar: (page: number) => void }) {
  if (pagina.totalPages <= 1) return null;

  return (
    <nav aria-label="Paginación" className="flex items-center justify-between gap-3">
      <button type="button" className={boton} disabled={!pagina.hasPrevious} onClick={() => onCambiar(pagina.page - 1)}>
        Anterior
      </button>
      <span className="text-sm text-zinc-500">
        Página {pagina.page} de {pagina.totalPages}
      </span>
      <button type="button" className={boton} disabled={!pagina.hasNext} onClick={() => onCambiar(pagina.page + 1)}>
        Siguiente
      </button>
    </nav>
  );
}
