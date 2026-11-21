"use client";

import { useEffect, useRef, useState, type PointerEvent, type ReactNode } from "react";
import { useAuth } from "@/features/auth/AuthProvider";
import { CATEGORIAS, ESTADOS, FRANJAS, PUNTOS, fechaCorta, hallazgosApi, type EstadoObjeto, type HallazgoPublico } from "./api";

/**
 * Foto de un hallazgo. Se pide con el token (no es una URL pública) y se muestra desde memoria.
 * Usa object-cover dentro de un recuadro fijo para no deformar la imagen; al tocarla se abre entera y grande.
 */
export function FotoHallazgo({ id, titulo }: { id: string; titulo: string }) {
  const { obtenerToken } = useAuth();
  const [url, setUrl] = useState<string | null>(null);
  const ampliada = useRef<HTMLDialogElement>(null);
  // Cada vez que se abre, el visor empieza sin zoom.
  const [aperturas, setAperturas] = useState(0);

  function abrir() {
    setAperturas((n) => n + 1);
    ampliada.current?.showModal();
  }

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
    <>
      <div className="relative aspect-[4/3] w-full overflow-hidden rounded-lg bg-zinc-100 dark:bg-zinc-800">
        {url && (
          // eslint-disable-next-line @next/next/no-img-element -- blob protegido; con teclado se usa el botón "Ver en grande".
          <img
            src={url}
            alt={`Foto de ${titulo}`}
            onClick={abrir}
            className="h-full w-full cursor-zoom-in object-cover transition-transform hover:scale-[1.03]"
          />
        )}
        {url && (
          <button
            type="button"
            onClick={abrir}
            className="absolute right-2 bottom-2 rounded-full bg-black/65 px-3 py-1 text-xs font-medium text-white hover:bg-black/80"
          >
            Ver en grande
          </button>
        )}
      </div>
      {url && (
        <dialog
          ref={ampliada}
          aria-label={`Foto de ${titulo}`}
          // Un clic afuera de la foto (sobre el fondo oscuro) también la cierra.
          onClick={(e) => e.target === e.currentTarget && e.currentTarget.close()}
          className="m-auto max-h-[92vh] max-w-[94vw] overflow-visible bg-transparent p-0 backdrop:bg-black/80"
        >
          <VisorConZoom key={aperturas} url={url} titulo={titulo} onCerrar={() => ampliada.current?.close()} />
        </dialog>
      )}
    </>
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
    <article aria-label={hallazgo.tituloPublico} className="flex min-w-0 flex-col gap-3 rounded-xl border bg-superficie border-zinc-200 p-4 dark:border-zinc-800">
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

const ZOOM_MAXIMO = 6;
const limitar = (valor: number, minimo: number, maximo: number) => Math.min(maximo, Math.max(minimo, valor));

/**
 * Foto ampliada con zoom para ver detalles: botones + y −, la ruedita del mouse, doble clic, o pellizcar con dos dedos
 * en el celular. Con zoom, se arrastra para moverse por la foto.
 */
function VisorConZoom({ url, titulo, onCerrar }: { url: string; titulo: string; onCerrar: () => void }) {
  const [zoom, setZoom] = useState(1);
  const [desplazamiento, setDesplazamiento] = useState({ x: 0, y: 0 });
  const marco = useRef<HTMLDivElement>(null);
  // Dedos o mouse apoyados sobre la foto (para arrastrar o pellizcar).
  const punteros = useRef(new Map<number, { x: number; y: number }>());
  const distanciaInicial = useRef<number | null>(null);

  /** Que la foto no se pueda arrastrar más allá de sus bordes (el desplazamiento está en píxeles de la foto sin zoom). */
  function dentroDelMarco(d: { x: number; y: number }, conZoom: number) {
    const ancho = marco.current?.clientWidth ?? 0;
    const alto = marco.current?.clientHeight ?? 0;
    const maxX = (ancho * (conZoom - 1)) / (2 * conZoom);
    const maxY = (alto * (conZoom - 1)) / (2 * conZoom);
    return { x: limitar(d.x, -maxX, maxX), y: limitar(d.y, -maxY, maxY) };
  }

  function cambiarZoom(nuevo: number) {
    const valor = limitar(nuevo, 1, ZOOM_MAXIMO);
    setZoom(valor);
    setDesplazamiento((d) => dentroDelMarco(d, valor));
  }

  // La ruedita usa siempre el zoom actual (el listener se registra una sola vez).
  const ultimoZoom = useRef((factor: number) => cambiarZoom(zoom * factor));
  useEffect(() => {
    ultimoZoom.current = (factor: number) => cambiarZoom(zoom * factor);
  });

  // La ruedita se escucha a mano: React registra "wheel" como pasivo y no deja frenar el desplazamiento de la página.
  useEffect(() => {
    const elemento = marco.current;
    if (!elemento) return;
    const rueda = (evento: WheelEvent) => {
      evento.preventDefault();
      ultimoZoom.current(evento.deltaY < 0 ? 1.2 : 1 / 1.2);
    };
    elemento.addEventListener("wheel", rueda, { passive: false });
    return () => elemento.removeEventListener("wheel", rueda);
  }, []);

  function apoyar(evento: PointerEvent<HTMLDivElement>) {
    evento.currentTarget.setPointerCapture(evento.pointerId);
    punteros.current.set(evento.pointerId, { x: evento.clientX, y: evento.clientY });
    distanciaInicial.current = null;
  }

  function mover(evento: PointerEvent<HTMLDivElement>) {
    const anterior = punteros.current.get(evento.pointerId);
    if (!anterior) return;
    const actual = { x: evento.clientX, y: evento.clientY };
    punteros.current.set(evento.pointerId, actual);

    if (punteros.current.size === 2) {
      const [a, b] = [...punteros.current.values()];
      const distancia = Math.hypot(a.x - b.x, a.y - b.y);
      if (distanciaInicial.current) cambiarZoom(zoom * (distancia / distanciaInicial.current));
      distanciaInicial.current = distancia;
    } else if (zoom > 1) {
      setDesplazamiento((d) => dentroDelMarco({ x: d.x + (actual.x - anterior.x) / zoom, y: d.y + (actual.y - anterior.y) / zoom }, zoom));
    }
  }

  function soltar(evento: PointerEvent<HTMLDivElement>) {
    punteros.current.delete(evento.pointerId);
    distanciaInicial.current = null;
  }

  const botonZoom = "flex size-9 items-center justify-center rounded-full bg-white text-lg font-bold text-indigo-950 hover:bg-zinc-200 disabled:opacity-40";

  return (
    <div className="flex flex-col items-center gap-3">
      <div
        ref={marco}
        onPointerDown={apoyar}
        onPointerMove={mover}
        onPointerUp={soltar}
        onPointerCancel={soltar}
        onDoubleClick={() => cambiarZoom(zoom > 1 ? 1 : 2.5)}
        className={`max-h-[78vh] max-w-[94vw] touch-none overflow-hidden rounded-lg shadow-2xl select-none ${zoom > 1 ? "cursor-grab active:cursor-grabbing" : "cursor-zoom-in"}`}
      >
        {/* eslint-disable-next-line @next/next/no-img-element -- la imagen viene de un blob protegido. */}
        <img
          src={url}
          alt={`Foto de ${titulo}`}
          draggable={false}
          style={{ transform: `scale(${zoom}) translate(${desplazamiento.x}px, ${desplazamiento.y}px)` }}
          className="block max-h-[78vh] max-w-[94vw] object-contain transition-transform duration-75"
        />
      </div>
      <div className="flex flex-wrap items-center justify-center gap-2">
        <button type="button" className={botonZoom} onClick={() => cambiarZoom(zoom / 1.5)} disabled={zoom <= 1} aria-label="Alejar">
          −
        </button>
        <span className="min-w-14 text-center text-sm font-medium text-white" aria-live="polite">
          {Math.round(zoom * 100)} %
        </span>
        <button type="button" className={botonZoom} onClick={() => cambiarZoom(zoom * 1.5)} disabled={zoom >= ZOOM_MAXIMO} aria-label="Acercar">
          +
        </button>
        <span className="mx-2 font-medium text-white">{titulo}</span>
        <button type="button" onClick={onCerrar} className="rounded-full bg-white px-4 py-1.5 text-sm font-semibold text-indigo-950 hover:bg-zinc-200" autoFocus>
          Cerrar
        </button>
      </div>
      <p className="text-xs text-zinc-300">Acercá con + y −, la ruedita del mouse, doble clic o pellizcando. Con zoom, arrastrá para moverte.</p>
    </div>
  );
}
