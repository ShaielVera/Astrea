"use client";

import { useId, useMemo, useRef, useState, type KeyboardEvent } from "react";

/** `otrosNombres`: otras formas de llamarla, que también la encuentran (ej.: "Villa Soledad" → Soledad). */
export type Opcion = { valor: string; detalle?: string; otrosNombres?: string[] };

function nombres(opcion: Opcion): string[] {
  return [opcion.valor, ...(opcion.otrosNombres ?? [])].map(sinTildes);
}

/** "Sán  JUSTO" → "san justo": para comparar sin mayúsculas, tildes ni espacios de más. */
export function sinTildes(texto: string): string {
  return texto.normalize("NFD").replace(/\p{M}/gu, "").toLowerCase().replace(/\s+/g, " ").trim();
}

type Props = {
  etiqueta: string;
  opciones: Opcion[];
  valor: string | null;
  onElegir: (valor: string | null) => void;
  placeholder?: string;
  ayuda?: string;
  sinResultados?: string;
};

/**
 * Campo para elegir una opción de una lista larga: se escribe para filtrar (primero las que empiezan así) o se abre
 * la lista con la flecha. Solo acepta opciones de la lista, así nadie escribe "san justo" y "San Justo" por separado.
 */
export function BuscadorDeOpciones({ etiqueta, opciones, valor, onElegir, placeholder, ayuda, sinResultados = "No hay coincidencias." }: Props) {
  const id = useId();
  const [texto, setTexto] = useState(valor ?? "");
  const [abierto, setAbierto] = useState(false);
  const [resaltada, setResaltada] = useState(0);
  const lista = useRef<HTMLUListElement>(null);

  const filtradas = useMemo(() => {
    const buscado = sinTildes(texto);
    if (!buscado || buscado === sinTildes(valor ?? "")) return opciones;
    const empiezan = opciones.filter((o) => nombres(o).some((n) => n.startsWith(buscado)));
    const contienen = opciones.filter((o) => !empiezan.includes(o) && nombres(o).some((n) => n.includes(buscado)));
    return [...empiezan, ...contienen];
  }, [texto, valor, opciones]);

  function elegir(opcion: Opcion) {
    onElegir(opcion.valor);
    setTexto(opcion.valor);
    setAbierto(false);
  }

  function mover(paso: number) {
    if (!abierto) setAbierto(true);
    const siguiente = Math.max(0, Math.min(filtradas.length - 1, resaltada + paso));
    setResaltada(siguiente);
    lista.current?.children[siguiente]?.scrollIntoView({ block: "nearest" });
  }

  function teclado(evento: KeyboardEvent<HTMLInputElement>) {
    if (evento.key === "ArrowDown") {
      evento.preventDefault();
      mover(1);
    } else if (evento.key === "ArrowUp") {
      evento.preventDefault();
      mover(-1);
    } else if (evento.key === "Enter" && abierto && filtradas[resaltada]) {
      evento.preventDefault();
      elegir(filtradas[resaltada]);
    } else if (evento.key === "Escape") {
      setAbierto(false);
    }
  }

  /** Al salir del campo: si escribió exactamente una opción (sin importar mayúsculas) se toma; si no, vuelve a la elegida. */
  function alSalir() {
    setAbierto(false);
    const exacta = opciones.find((o) => nombres(o).includes(sinTildes(texto)));
    if (exacta) {
      elegir(exacta);
    } else if (!texto.trim()) {
      onElegir(null);
    } else {
      setTexto(valor ?? "");
    }
  }

  return (
    <div className="relative flex min-w-0 flex-col gap-1 text-sm font-medium">
      <label htmlFor={`${id}-campo`}>{etiqueta}</label>
      <div className="relative">
        <input
          id={`${id}-campo`}
          role="combobox"
          aria-expanded={abierto}
          aria-controls={`${id}-lista`}
          aria-autocomplete="list"
          aria-activedescendant={abierto && filtradas[resaltada] ? `${id}-op-${resaltada}` : undefined}
          aria-describedby={ayuda ? `${id}-ayuda` : undefined}
          autoComplete="off"
          value={texto}
          placeholder={placeholder}
          onChange={(e) => {
            setTexto(e.target.value);
            setResaltada(0);
            setAbierto(true);
          }}
          onFocus={(e) => e.target.select()}
          onKeyDown={teclado}
          onBlur={alSalir}
          className="w-full min-w-0 rounded-md border border-zinc-300 bg-transparent py-2 pr-10 pl-3 font-normal dark:border-zinc-700 dark:bg-zinc-900"
        />
        <button
          type="button"
          tabIndex={-1}
          aria-label={abierto ? `Cerrar la lista de ${etiqueta.toLowerCase()}` : `Abrir la lista de ${etiqueta.toLowerCase()}`}
          // mousedown en vez de click: así el campo no pierde el foco (y no se cierra la lista) antes de abrirla.
          onMouseDown={(e) => {
            e.preventDefault();
            setResaltada(0);
            setAbierto((actual) => !actual);
            (e.currentTarget.previousElementSibling as HTMLInputElement | null)?.focus();
          }}
          className="absolute inset-y-0 right-0 flex w-10 items-center justify-center text-zinc-500 hover:text-zinc-800 dark:hover:text-zinc-200"
        >
          <svg aria-hidden="true" viewBox="0 0 20 20" className={`size-4 transition-transform ${abierto ? "rotate-180" : ""}`} fill="currentColor">
            <path d="M5.3 7.3a1 1 0 0 1 1.4 0L10 10.6l3.3-3.3a1 1 0 1 1 1.4 1.4l-4 4a1 1 0 0 1-1.4 0l-4-4a1 1 0 0 1 0-1.4z" />
          </svg>
        </button>
      </div>
      {ayuda && (
        <span id={`${id}-ayuda`} className="text-xs font-normal text-zinc-500">
          {ayuda}
        </span>
      )}
      {abierto && (
        <ul
          ref={lista}
          id={`${id}-lista`}
          role="listbox"
          aria-label={etiqueta}
          className="absolute top-full right-0 left-0 z-20 mt-1 max-h-64 overflow-y-auto rounded-md border border-zinc-200 bg-superficie py-1 font-normal shadow-lg dark:border-zinc-700"
        >
          {filtradas.length === 0 ? (
            <li className="px-3 py-2 text-zinc-500">{sinResultados}</li>
          ) : (
            filtradas.map((opcion, indice) => (
              <li
                key={opcion.valor}
                id={`${id}-op-${indice}`}
                role="option"
                aria-selected={opcion.valor === valor}
                onMouseDown={(e) => {
                  e.preventDefault();
                  elegir(opcion);
                }}
                onMouseEnter={() => setResaltada(indice)}
                className={`flex cursor-pointer items-baseline justify-between gap-3 px-3 py-2 ${indice === resaltada ? "bg-indigo-50 dark:bg-indigo-950" : ""}`}
              >
                <span className={opcion.valor === valor ? "font-semibold" : ""}>{opcion.valor}</span>
                <span className="text-right text-xs text-zinc-500">
                  {opcion.otrosNombres?.length ? `También: ${opcion.otrosNombres.join(", ")} · ` : ""}
                  {opcion.detalle}
                </span>
              </li>
            ))
          )}
        </ul>
      )}
    </div>
  );
}
