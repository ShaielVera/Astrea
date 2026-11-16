"use client";

import Link from "next/link";
import { useCallback, useEffect, useRef, useState, type FormEvent, type KeyboardEvent } from "react";
import { useAuth } from "@/features/auth/AuthProvider";
import { mensajeDeError } from "@/features/auth/mensajes-error";
import {
  asistenteApi,
  LARGO_MAXIMO_PREGUNTA,
  paginas,
  type ConversacionResumen,
  type Fuente,
  type Intercambio,
  type Mensaje,
} from "@/features/asistente/api";

const EJEMPLOS = [
  "¿Cuándo cierra la inscripción a mesas de examen?",
  "¿Cuánto tiempo dura la regularidad de una materia?",
  "¿Cuándo son las próximas mesas de examen?",
  "Quiero pedir una constancia de alumno regular",
];

const boton = "rounded-md border border-zinc-300 px-3 py-1.5 text-sm font-medium hover:bg-zinc-100 disabled:opacity-50 dark:border-zinc-700 dark:hover:bg-zinc-800";

export default function AsistentePage() {
  const { obtenerToken } = useAuth();
  const [conversaciones, setConversaciones] = useState<ConversacionResumen[] | null>(null);
  const [actual, setActual] = useState<{ id: string; titulo: string } | null>(null);
  const [mensajes, setMensajes] = useState<Mensaje[]>([]);
  const [pendiente, setPendiente] = useState<string | null>(null);
  const [texto, setTexto] = useState("");
  const [confirmarBorrado, setConfirmarBorrado] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const campoPregunta = useRef<HTMLTextAreaElement>(null);
  const finDelChat = useRef<HTMLDivElement>(null);

  const cargarConversaciones = useCallback(async () => {
    const pagina = await asistenteApi.conversaciones(await obtenerToken());
    setConversaciones(pagina.items);
  }, [obtenerToken]);

  useEffect(() => {
    let vigente = true;
    obtenerToken()
      .then((token) => asistenteApi.conversaciones(token))
      .then((pagina) => vigente && setConversaciones(pagina.items))
      .catch((e) => vigente && setError(mensajeDeError(e)));
    return () => {
      vigente = false;
    };
  }, [obtenerToken]);

  useEffect(() => {
    finDelChat.current?.scrollIntoView?.({ block: "end" });
  }, [mensajes, pendiente]);

  function nueva() {
    setActual(null);
    setMensajes([]);
    setError(null);
    setConfirmarBorrado(false);
    campoPregunta.current?.focus();
  }

  async function abrir(id: string) {
    setError(null);
    setConfirmarBorrado(false);
    try {
      const conversacion = await asistenteApi.conversacion(await obtenerToken(), id);
      setActual({ id: conversacion.id, titulo: conversacion.titulo });
      setMensajes(conversacion.mensajes);
    } catch (e) {
      setError(mensajeDeError(e));
    }
  }

  async function borrar() {
    if (!actual) return;
    setError(null);
    try {
      await asistenteApi.eliminar(await obtenerToken(), actual.id);
      nueva();
      await cargarConversaciones();
    } catch (e) {
      setError(mensajeDeError(e));
    }
  }

  async function preguntar(pregunta: string) {
    const limpia = pregunta.trim();
    if (!limpia || pendiente) return;
    setError(null);
    setPendiente(limpia);
    setTexto("");
    let intercambio: Intercambio;
    try {
      intercambio = await asistenteApi.preguntar(await obtenerToken(), limpia, actual?.id ?? null);
    } catch (e) {
      // La pregunta no se perdió: vuelve al campo para reintentar.
      setPendiente(null);
      setTexto(limpia);
      setError(mensajeDeError(e));
      campoPregunta.current?.focus();
      return;
    }

    // En el mismo render: la pregunta pendiente se reemplaza por la guardada, sin mostrarse dos veces.
    setPendiente(null);
    setActual({ id: intercambio.conversacionId, titulo: intercambio.titulo });
    setMensajes((anteriores) => [...anteriores, intercambio.pregunta, intercambio.respuesta]);
    campoPregunta.current?.focus();
    cargarConversaciones().catch((e) => setError(mensajeDeError(e)));
  }

  function enviar(evento: FormEvent) {
    evento.preventDefault();
    void preguntar(texto);
  }

  // Enter envía; Shift+Enter hace un salto de línea.
  function teclas(evento: KeyboardEvent<HTMLTextAreaElement>) {
    if (evento.key === "Enter" && !evento.shiftKey && !evento.nativeEvent.isComposing) {
      evento.preventDefault();
      void preguntar(texto);
    }
  }

  async function resolverPropuesta(mensaje: Mensaje, accion: "confirmar" | "cancelar") {
    if (!actual) return;
    setError(null);
    try {
      const token = await obtenerToken();
      const actualizado =
        accion === "confirmar"
          ? await asistenteApi.confirmar(token, actual.id, mensaje.id)
          : await asistenteApi.cancelar(token, actual.id, mensaje.id);
      setMensajes((anteriores) => anteriores.map((m) => (m.id === actualizado.id ? actualizado : m)));
    } catch (e) {
      setError(mensajeDeError(e));
    }
  }

  async function abrirFuente(fuente: Fuente) {
    setError(null);
    try {
      await asistenteApi.abrirFuente(await obtenerToken(), fuente);
    } catch (e) {
      setError(mensajeDeError(e));
    }
  }

  return (
    <main className="mx-auto flex w-full max-w-5xl flex-1 flex-col gap-6 px-4 py-10 sm:px-6">
      <div className="flex flex-col gap-1">
        <h1 className="text-3xl font-semibold tracking-tight">Asistente</h1>
        <p className="text-zinc-600 dark:text-zinc-400">
          Preguntá sobre reglamentos, mesas de examen, correlatividades, objetos perdidos o tus trámites. Respondo solo con los documentos
          oficiales y los datos actuales de Astrea, y te muestro de dónde saqué cada dato. También puedo prepararte una solicitud de trámite
          para que la confirmes.
        </p>
      </div>

      <div role="note" aria-label="Antes de preguntar" className="rounded-md bg-amber-50 px-3 py-2 text-sm text-amber-900 dark:bg-amber-950 dark:text-amber-200">
        <p>
          Las respuestas las redacta una inteligencia artificial y pueden tener errores: ante cualquier duda, confirmá en Secretaría. El
          asistente no aprueba trámites ni envía nada sin que lo confirmes.
        </p>
        <p className="mt-1">
          No escribas datos personales (DNI, teléfono, email): no hacen falta y se ocultan automáticamente. Tus conversaciones las ves solo
          vos y se borran a los 30 días sin uso.
        </p>
      </div>

      {error && (
        <p role="alert" className="rounded-md bg-red-50 px-3 py-2 text-sm text-red-700 dark:bg-red-950 dark:text-red-300">
          {error}
        </p>
      )}

      <div className="grid grid-cols-1 gap-6 lg:grid-cols-[minmax(0,1fr)_minmax(0,3fr)]">
        <nav aria-labelledby="conversaciones-titulo" className="flex min-w-0 flex-col gap-3">
          <div className="flex items-center justify-between gap-2">
            <h2 id="conversaciones-titulo" className="text-lg font-semibold">
              Conversaciones
            </h2>
            <button type="button" onClick={nueva} className={boton}>
              Nueva
            </button>
          </div>
          {conversaciones?.length === 0 && <p className="text-sm text-zinc-500">Todavía no hiciste ninguna pregunta.</p>}
          <ul className="flex flex-col gap-1">
            {conversaciones?.map((conversacion) => (
              <li key={conversacion.id}>
                <button
                  type="button"
                  onClick={() => abrir(conversacion.id)}
                  aria-current={actual?.id === conversacion.id ? "true" : undefined}
                  className="w-full truncate rounded-md px-2 py-1.5 text-left text-sm hover:bg-zinc-100 aria-[current=true]:bg-indigo-50 aria-[current=true]:font-medium dark:hover:bg-zinc-800 dark:aria-[current=true]:bg-indigo-950"
                >
                  {conversacion.titulo}
                </button>
              </li>
            ))}
          </ul>
        </nav>

        <section aria-label="Chat con el asistente" className="flex min-w-0 flex-col gap-4 rounded-xl border border-zinc-200 p-4 dark:border-zinc-800">
          {actual && (
            <div className="flex flex-wrap items-center justify-between gap-2 border-b border-zinc-200 pb-3 dark:border-zinc-800">
              <h2 className="min-w-0 truncate font-medium">{actual.titulo}</h2>
              {confirmarBorrado ? (
                <div className="flex items-center gap-2 text-sm">
                  <span>¿Borrar esta conversación?</span>
                  <button type="button" onClick={borrar} className="rounded-md bg-red-600 px-3 py-1.5 font-medium text-white hover:bg-red-700">
                    Sí, borrar
                  </button>
                  <button type="button" onClick={() => setConfirmarBorrado(false)} className={boton}>
                    Cancelar
                  </button>
                </div>
              ) : (
                <button type="button" onClick={() => setConfirmarBorrado(true)} className={boton}>
                  Borrar conversación
                </button>
              )}
            </div>
          )}

          {mensajes.length === 0 && !pendiente && (
            <div className="flex flex-col gap-2">
              <p className="text-sm text-zinc-600 dark:text-zinc-400">Podés empezar con alguna de estas preguntas:</p>
              <ul className="flex flex-wrap gap-2">
                {EJEMPLOS.map((ejemplo) => (
                  <li key={ejemplo}>
                    <button type="button" onClick={() => preguntar(ejemplo)} className={`${boton} text-left font-normal`}>
                      {ejemplo}
                    </button>
                  </li>
                ))}
              </ul>
            </div>
          )}

          <ol role="log" aria-label="Mensajes" aria-live="polite" className="flex flex-col gap-4">
            {mensajes.map((mensaje) =>
              mensaje.rol === "Usuario" ? (
                <li key={mensaje.id} aria-label="Tu pregunta" className="ml-auto max-w-[85%] whitespace-pre-wrap rounded-xl bg-indigo-600 px-3 py-2 text-white">
                  {mensaje.contenido}
                </li>
              ) : (
                <RespuestaAsistente key={mensaje.id} mensaje={mensaje} onAbrirFuente={abrirFuente} onPropuesta={resolverPropuesta} />
              ),
            )}
            {pendiente && (
              <li aria-label="Tu pregunta" className="ml-auto max-w-[85%] whitespace-pre-wrap rounded-xl bg-indigo-600 px-3 py-2 text-white opacity-70">
                {pendiente}
              </li>
            )}
          </ol>
          {pendiente && (
            <p role="status" className="text-sm text-zinc-500">
              Buscando en los documentos oficiales…
            </p>
          )}
          <div ref={finDelChat} />

          <form onSubmit={enviar} aria-label="Hacer una pregunta" className="flex flex-col gap-2 border-t border-zinc-200 pt-3 dark:border-zinc-800">
            <label htmlFor="pregunta" className="text-sm font-medium">
              Tu pregunta
            </label>
            <textarea
              id="pregunta"
              ref={campoPregunta}
              value={texto}
              onChange={(e) => setTexto(e.target.value)}
              onKeyDown={teclas}
              maxLength={LARGO_MAXIMO_PREGUNTA}
              rows={2}
              aria-describedby="pregunta-ayuda"
              placeholder="Ej.: ¿Cuántas faltas puedo tener para mantener la regularidad?"
              className="w-full min-w-0 resize-y rounded-md border border-zinc-300 bg-transparent px-3 py-2 dark:border-zinc-700 dark:bg-zinc-900"
            />
            <div className="flex items-center justify-between gap-2">
              <span id="pregunta-ayuda" className="text-xs text-zinc-500">
                Enter para enviar, Shift+Enter para un salto de línea · {texto.length}/{LARGO_MAXIMO_PREGUNTA}
              </span>
              <button
                type="submit"
                disabled={!texto.trim() || pendiente !== null}
                className="rounded-md bg-indigo-600 px-4 py-2 text-sm font-medium text-white hover:bg-indigo-700 disabled:opacity-50"
              >
                Enviar
              </button>
            </div>
          </form>
        </section>
      </div>
    </main>
  );
}

function RespuestaAsistente({
  mensaje,
  onAbrirFuente,
  onPropuesta,
}: {
  mensaje: Mensaje;
  onAbrirFuente: (fuente: Fuente) => void;
  onPropuesta: (mensaje: Mensaje, accion: "confirmar" | "cancelar") => Promise<void>;
}) {
  return (
    <li aria-label="Respuesta del asistente" className="mr-auto flex max-w-[95%] flex-col gap-2 rounded-xl bg-zinc-100 px-3 py-2 dark:bg-zinc-800">
      {mensaje.sinEvidencia && (
        <span className="w-fit rounded-full bg-amber-100 px-2 py-0.5 text-xs font-medium text-amber-900 dark:bg-amber-900 dark:text-amber-100">
          Sin información confirmada
        </span>
      )}
      <p className="whitespace-pre-wrap">
        <ConCitas texto={mensaje.contenido} prefijo={mensaje.id} />
      </p>
      {mensaje.propuesta && <TarjetaPropuesta mensaje={mensaje} onAccion={onPropuesta} />}
      {mensaje.fuentes.length > 0 && (
        <div className="flex flex-col gap-1 border-t border-zinc-200 pt-2 text-sm dark:border-zinc-700">
          <h3 className="font-medium">Fuentes</h3>
          <ol aria-label="Fuentes" className="flex flex-col gap-2">
            {mensaje.fuentes.map((fuente, i) => (
              <li key={`${fuente.documentoId ?? fuente.titulo}-${i}`} id={`${mensaje.id}-fuente-${i + 1}`} className="flex flex-col gap-1">
                <div className="flex flex-wrap items-baseline gap-x-2">
                  <span className="font-medium">
                    [{i + 1}] {fuente.titulo}
                    {fuente.version !== null && ` (versión ${fuente.version})`}
                  </span>
                  <span className="text-zinc-600 dark:text-zinc-400">
                    {[fuente.seccion, paginas(fuente), fuente.fuente].filter(Boolean).join(" · ")}
                  </span>
                </div>
                <blockquote className="border-l-2 border-zinc-300 pl-2 text-zinc-600 dark:border-zinc-600 dark:text-zinc-400">
                  {fuente.extracto}
                </blockquote>
                {fuente.tipo === "DatosDeAstrea" && fuente.enlace ? (
                  <Link href={fuente.enlace} className="w-fit text-indigo-600 hover:underline dark:text-indigo-400">
                    Ver en {fuente.titulo}
                  </Link>
                ) : (
                  <button
                    type="button"
                    onClick={() => onAbrirFuente(fuente)}
                    className="w-fit text-indigo-600 hover:underline dark:text-indigo-400"
                    aria-label={`Descargar el PDF de ${fuente.titulo}`}
                  >
                    Descargar PDF
                  </button>
                )}
              </li>
            ))}
          </ol>
        </div>
      )}
    </li>
  );
}

/** La solicitud que preparó el asistente. Solo se envía si el estudiante la confirma. */
function TarjetaPropuesta({ mensaje, onAccion }: { mensaje: Mensaje; onAccion: (mensaje: Mensaje, accion: "confirmar" | "cancelar") => Promise<void> }) {
  const propuesta = mensaje.propuesta!;
  const [enviando, setEnviando] = useState(false);

  async function ejecutar(accion: "confirmar" | "cancelar") {
    setEnviando(true);
    try {
      await onAccion(mensaje, accion);
    } finally {
      setEnviando(false);
    }
  }

  return (
    <section aria-label={`Solicitud: ${propuesta.tipoNombre}`} className="flex flex-col gap-2 rounded-lg border border-indigo-200 bg-white p-3 text-sm dark:border-indigo-900 dark:bg-zinc-900">
      <h3 className="font-medium">Solicitud: {propuesta.tipoNombre}</h3>
      <dl className="grid grid-cols-[auto_minmax(0,1fr)] gap-x-3 gap-y-1">
        {propuesta.resumen.map((dato) => (
          <div key={dato.etiqueta} className="contents">
            <dt className="text-zinc-500">{dato.etiqueta}</dt>
            <dd className="break-words">{dato.valor}</dd>
          </div>
        ))}
      </dl>
      {propuesta.estado === "Pendiente" && (
        <>
          <p className="text-zinc-600 dark:text-zinc-400">Todavía no se envió. Revisá los datos: se envía a Secretaría solo si confirmás.</p>
          <div className="flex flex-wrap gap-2">
            <button
              type="button"
              disabled={enviando}
              onClick={() => ejecutar("confirmar")}
              className="rounded-md bg-indigo-600 px-3 py-1.5 font-medium text-white hover:bg-indigo-700 disabled:opacity-50"
            >
              Confirmar y enviar
            </button>
            <button type="button" disabled={enviando} onClick={() => ejecutar("cancelar")} className={boton}>
              Cancelar
            </button>
          </div>
        </>
      )}
      {propuesta.estado === "Confirmada" && (
        <p role="status" className="text-emerald-700 dark:text-emerald-300">
          Enviada: trámite {propuesta.numeroTramite}.{" "}
          <Link href="/tramites" className="font-medium underline">
            Seguilo en Mis trámites
          </Link>
        </p>
      )}
      {propuesta.estado === "Cancelada" && (
        <p role="status" className="text-zinc-500">
          Cancelaste esta solicitud. No se envió nada.
        </p>
      )}
    </section>
  );
}

/** Convierte las citas [1], [2]… en enlaces a la fuente correspondiente. */
function ConCitas({ texto, prefijo }: { texto: string; prefijo: string }) {
  const partes = texto.split(/(\[\d+\])/);
  return (
    <>
      {partes.map((parte, i) => {
        const cita = /^\[(\d+)\]$/.exec(parte);
        return cita ? (
          <sup key={i}>
            <a href={`#${prefijo}-fuente-${cita[1]}`} className="text-indigo-600 hover:underline dark:text-indigo-400" aria-label={`Fuente ${cita[1]}`}>
              [{cita[1]}]
            </a>
          </sup>
        ) : (
          parte
        );
      })}
    </>
  );
}
