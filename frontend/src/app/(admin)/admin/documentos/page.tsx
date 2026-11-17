"use client";

import Link from "next/link";
import { useCallback, useEffect, useRef, useState, type FormEvent, type RefObject } from "react";
import { useAuth } from "@/features/auth/AuthProvider";
import { mensajeDeError } from "@/features/auth/mensajes-error";
import type { Pagina } from "@/features/academico/api";
import { Paginacion } from "@/features/mesas/Paginacion";
import { fechaHora } from "@/features/tramites/api";
import {
  EN_PROCESO,
  ESTADOS,
  PDF_MAXIMO_BYTES,
  TIPOS,
  documentosRagApi,
  type DocumentoRag,
  type EstadoIndexacion,
  type Fragmento,
  type TipoDocumentoRag,
} from "@/features/documentos-rag/api";

const campo = "w-full min-w-0 rounded-md border border-zinc-300 bg-transparent px-3 py-2 font-normal dark:border-zinc-700 dark:bg-zinc-900";
const botonChico =
  "rounded-md border border-zinc-300 px-3 py-1.5 text-sm font-medium hover:bg-zinc-100 disabled:opacity-60 dark:border-zinc-700 dark:hover:bg-zinc-800";
const botonPrimario = "rounded-md bg-indigo-600 px-4 py-2 font-medium text-white hover:bg-indigo-700 disabled:opacity-60";
const botonPeligroChico =
  "rounded-md border border-red-300 px-3 py-1.5 text-sm font-medium text-red-700 hover:bg-red-50 dark:border-red-900 dark:text-red-300 dark:hover:bg-red-950";

const ESTILO_ESTADO: Record<EstadoIndexacion, string> = {
  Pendiente: "bg-amber-100 text-amber-900 dark:bg-amber-950 dark:text-amber-200",
  Procesando: "bg-sky-100 text-sky-900 dark:bg-sky-950 dark:text-sky-200",
  Indexado: "bg-emerald-100 text-emerald-900 dark:bg-emerald-950 dark:text-emerald-200",
  Error: "bg-red-100 text-red-900 dark:bg-red-950 dark:text-red-200",
  Reemplazado: "bg-zinc-200 text-zinc-700 dark:bg-zinc-800 dark:text-zinc-300",
  Retirado: "bg-zinc-200 text-zinc-700 dark:bg-zinc-800 dark:text-zinc-300",
};

/** Cada cuánto se vuelve a consultar mientras hay documentos en proceso. */
const INTERVALO_MS = 2000;

type Filtro = { estado?: EstadoIndexacion; texto?: string };

export default function DocumentosDelAsistente() {
  const { obtenerToken } = useAuth();
  const [filtro, setFiltro] = useState<Filtro>({});
  const [aplicado, setAplicado] = useState<{ filtro: Filtro; page: number }>({ filtro: {}, page: 1 });
  const [pagina, setPagina] = useState<Pagina<DocumentoRag> | null>(null);
  const [versionDe, setVersionDe] = useState<DocumentoRag | null>(null);
  const [aviso, setAviso] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);
  const ultimaCarga = useRef(0);
  const formulario = useRef<HTMLFormElement>(null);

  const cargar = useCallback(
    async (criterio: Filtro, page: number) => {
      const numero = ++ultimaCarga.current;
      try {
        const resultado = await documentosRagApi.listar(await obtenerToken(), { ...criterio, page });
        if (numero !== ultimaCarga.current) return;
        setPagina(resultado);
        setAplicado({ filtro: criterio, page });
      } catch (e) {
        if (numero === ultimaCarga.current) setError(mensajeDeError(e));
      }
    },
    [obtenerToken],
  );

  useEffect(() => {
    let vigente = true;
    const numero = ++ultimaCarga.current;
    obtenerToken()
      .then((token) => documentosRagApi.listar(token, {}))
      .then((resultado) => vigente && numero === ultimaCarga.current && setPagina(resultado))
      .catch((e) => vigente && setError(mensajeDeError(e)));
    return () => {
      vigente = false;
    };
  }, [obtenerToken]);

  // Mientras haya documentos pendientes o procesándose, la lista se actualiza sola.
  const hayEnProceso = pagina?.items.some((documento) => EN_PROCESO.includes(documento.estado)) ?? false;
  useEffect(() => {
    if (!hayEnProceso) return;
    const temporizador = setTimeout(() => void cargar(aplicado.filtro, aplicado.page), INTERVALO_MS);
    return () => clearTimeout(temporizador);
  }, [hayEnProceso, pagina, aplicado, cargar]);

  async function accion(hacer: (token: string) => Promise<DocumentoRag>, mensaje: (documento: DocumentoRag) => string) {
    setError(null);
    setAviso(null);
    try {
      setAviso(mensaje(await hacer(await obtenerToken())));
      await cargar(aplicado.filtro, aplicado.page);
    } catch (e) {
      setError(mensajeDeError(e));
    }
  }

  return (
    <main className="mx-auto flex w-full max-w-5xl flex-1 flex-col gap-6 px-4 py-10 sm:px-6">
      <div className="flex flex-col gap-1">
        <Link href="/admin" className="text-sm text-indigo-600 hover:underline dark:text-indigo-400">
          ← Administración
        </Link>
        <h1 className="text-3xl font-semibold tracking-tight">Documentos del asistente</h1>
        <p className="text-zinc-600 dark:text-zinc-400">
          Reglamentos, planes, calendarios y comunicados que va a consultar el asistente. Cada PDF se lee, se divide en fragmentos y se indexa
          para buscarlo por significado. Una versión nueva reemplaza a la anterior recién cuando termina de indexarse.
        </p>
      </div>

      {aviso && (
        <p role="status" className="rounded-md bg-emerald-50 px-3 py-2 text-sm text-emerald-800 dark:bg-emerald-950 dark:text-emerald-200">
          {aviso}
        </p>
      )}
      {error && (
        <p role="alert" className="rounded-md bg-red-50 px-3 py-2 text-sm text-red-700 dark:bg-red-950 dark:text-red-300">
          {error}
        </p>
      )}

      <FormularioCarga
        key={versionDe?.id ?? "nuevo"}
        referencia={formulario}
        versionDe={versionDe}
        onCancelarVersion={() => setVersionDe(null)}
        onCargado={async (documento) => {
          setVersionDe(null);
          setError(null);
          setAviso(`Se cargó "${documento.titulo}" (versión ${documento.version}). Se está indexando…`);
          await cargar(aplicado.filtro, 1);
        }}
      />

      <section aria-labelledby="documentos-titulo" className="flex flex-col gap-4">
        <h2 id="documentos-titulo" className="text-lg font-semibold">
          Documentos cargados
        </h2>
        <form
          aria-label="Filtrar documentos"
          className="grid grid-cols-1 items-end gap-3 sm:grid-cols-[minmax(0,1fr)_minmax(0,2fr)_auto]"
          onSubmit={(e) => {
            e.preventDefault();
            void cargar(filtro, 1);
          }}
        >
          <label className="flex min-w-0 flex-col gap-1 text-sm font-medium">
            Estado
            <select
              value={filtro.estado ?? ""}
              onChange={(e) => setFiltro((f) => ({ ...f, estado: (e.target.value || undefined) as EstadoIndexacion | undefined }))}
              className={campo}
            >
              <option value="">Todos</option>
              {Object.entries(ESTADOS).map(([valor, etiqueta]) => (
                <option key={valor} value={valor}>
                  {etiqueta}
                </option>
              ))}
            </select>
          </label>
          <label className="flex min-w-0 flex-col gap-1 text-sm font-medium">
            Título
            <input
              type="search"
              value={filtro.texto ?? ""}
              onChange={(e) => setFiltro((f) => ({ ...f, texto: e.target.value || undefined }))}
              maxLength={100}
              className={campo}
            />
          </label>
          <button type="submit" className={botonChico}>
            Filtrar
          </button>
        </form>

        {pagina?.totalItems === 0 && <p className="text-sm text-zinc-500">No hay documentos con esos filtros.</p>}
        <div className="flex flex-col gap-4">
          {pagina?.items.map((documento) => (
            <TarjetaDocumento
              key={documento.id}
              documento={documento}
              onNuevaVersion={() => {
                setVersionDe(documento);
                formulario.current?.scrollIntoView({ behavior: "smooth" });
              }}
              onReindexar={() => accion((token) => documentosRagApi.reindexar(token, documento.id), (d) => `"${d.titulo}" volvió a la cola para indexarse.`)}
              onRetirar={() => accion((token) => documentosRagApi.retirar(token, documento.id), (d) => `"${d.titulo}" ya no se usa para responder.`)}
              onDescargar={async () => {
                try {
                  await documentosRagApi.descargar(await obtenerToken(), documento);
                } catch (e) {
                  setError(mensajeDeError(e));
                }
              }}
            />
          ))}
        </div>
        {pagina && <Paginacion pagina={pagina} onCambiar={(page) => void cargar(aplicado.filtro, page)} />}
      </section>
    </main>
  );
}

function FormularioCarga({
  referencia,
  versionDe,
  onCancelarVersion,
  onCargado,
}: {
  referencia: RefObject<HTMLFormElement | null>;
  versionDe: DocumentoRag | null;
  onCancelarVersion: () => void;
  onCargado: (documento: DocumentoRag) => Promise<void>;
}) {
  const { obtenerToken } = useAuth();
  const [archivo, setArchivo] = useState<File | null>(null);
  const [titulo, setTitulo] = useState(versionDe?.titulo ?? "");
  const [tipo, setTipo] = useState<TipoDocumentoRag>(versionDe?.tipo ?? "Reglamento");
  const [fuente, setFuente] = useState(versionDe?.fuente ?? "");
  const [fecha, setFecha] = useState("");
  const [enviando, setEnviando] = useState(false);
  const [error, setError] = useState<string | null>(null);

  function elegir(elegido: File | undefined) {
    setError(null);
    setArchivo(null);
    if (!elegido) return;
    if (elegido.type && elegido.type !== "application/pdf") {
      setError("El archivo tiene que ser un PDF.");
      return;
    }
    if (elegido.size > PDF_MAXIMO_BYTES) {
      setError("El PDF no puede superar los 20 MB.");
      return;
    }
    setArchivo(elegido);
    if (!titulo) setTitulo(elegido.name.replace(/\.pdf$/i, ""));
  }

  async function enviar(evento: FormEvent<HTMLFormElement>) {
    evento.preventDefault();
    if (!archivo) {
      setError("Elegí el archivo PDF del documento.");
      return;
    }
    setEnviando(true);
    setError(null);
    try {
      const documento = await documentosRagApi.cargar(await obtenerToken(), {
        archivo,
        titulo,
        tipo,
        fuente: fuente.trim() || undefined,
        fechaDocumento: fecha || undefined,
        nuevaVersionDe: versionDe?.id,
      });
      referencia.current?.reset();
      setArchivo(null);
      setTitulo("");
      setFuente("");
      setFecha("");
      await onCargado(documento);
    } catch (e) {
      setError(mensajeDeError(e));
    } finally {
      setEnviando(false);
    }
  }

  return (
    <form
      ref={referencia}
      onSubmit={enviar}
      aria-label="Cargar documento"
      className="flex flex-col gap-4 rounded-xl border bg-superficie border-indigo-200 p-4 dark:border-indigo-900"
    >
      <h2 className="text-lg font-semibold">{versionDe ? `Nueva versión de "${versionDe.titulo}"` : "Cargar documento"}</h2>
      {versionDe && (
        <p className="text-sm text-zinc-600 dark:text-zinc-400">
          La versión {versionDe.version} se sigue usando hasta que esta termine de indexarse.{" "}
          <button type="button" onClick={onCancelarVersion} className="text-indigo-600 underline dark:text-indigo-400">
            Cargar un documento distinto
          </button>
        </p>
      )}
      {error && (
        <p role="alert" className="rounded-md bg-red-50 px-3 py-2 text-sm text-red-700 dark:bg-red-950 dark:text-red-300">
          {error}
        </p>
      )}
      <div className="grid grid-cols-1 gap-3 sm:grid-cols-2">
        <label className="flex min-w-0 flex-col gap-1 text-sm font-medium sm:col-span-2">
          Archivo PDF
          <input
            type="file"
            accept="application/pdf,.pdf"
            required
            onChange={(e) => elegir(e.target.files?.[0])}
            aria-describedby="ayuda-pdf"
            className="text-sm font-normal file:mr-3 file:rounded-md file:border-0 file:bg-zinc-100 file:px-3 file:py-2 dark:file:bg-zinc-800"
          />
          <span id="ayuda-pdf" className="text-xs font-normal text-zinc-500">
            Hasta 20 MB. Tiene que tener texto seleccionable: un escaneo sin reconocimiento de texto (OCR) no se puede leer.
          </span>
        </label>
        <label className="flex min-w-0 flex-col gap-1 text-sm font-medium">
          Título
          <input value={titulo} onChange={(e) => setTitulo(e.target.value)} required maxLength={150} placeholder="Ej.: Reglamento Académico Marco" className={campo} />
        </label>
        <label className="flex min-w-0 flex-col gap-1 text-sm font-medium">
          Tipo
          <select value={tipo} onChange={(e) => setTipo(e.target.value as TipoDocumentoRag)} className={campo}>
            {Object.entries(TIPOS).map(([valor, etiqueta]) => (
              <option key={valor} value={valor}>
                {etiqueta}
              </option>
            ))}
          </select>
        </label>
        <label className="flex min-w-0 flex-col gap-1 text-sm font-medium">
          Fuente (opcional)
          <input value={fuente} onChange={(e) => setFuente(e.target.value)} maxLength={200} placeholder="Ej.: Resolución 4043/09" className={campo} />
        </label>
        <label className="flex min-w-0 flex-col gap-1 text-sm font-medium">
          Fecha del documento (opcional)
          <input type="date" value={fecha} onChange={(e) => setFecha(e.target.value)} className={campo} />
        </label>
      </div>
      <div>
        <button type="submit" className={botonPrimario} disabled={enviando}>
          {enviando ? "Cargando…" : "Cargar"}
        </button>
      </div>
    </form>
  );
}

function TarjetaDocumento({
  documento,
  onNuevaVersion,
  onReindexar,
  onRetirar,
  onDescargar,
}: {
  documento: DocumentoRag;
  onNuevaVersion: () => void;
  onReindexar: () => Promise<void>;
  onRetirar: () => Promise<void>;
  onDescargar: () => Promise<void>;
}) {
  const { obtenerToken } = useAuth();
  const [fragmentos, setFragmentos] = useState<Fragmento[] | null>(null);
  const [confirmarRetiro, setConfirmarRetiro] = useState(false);
  const enProceso = EN_PROCESO.includes(documento.estado);

  async function alternarFragmentos() {
    if (fragmentos) {
      setFragmentos(null);
      return;
    }
    const pagina = await documentosRagApi.fragmentos(await obtenerToken(), documento.id);
    setFragmentos(pagina.items);
  }

  return (
    <article aria-label={documento.titulo} className="flex min-w-0 flex-col gap-3 rounded-xl border bg-superficie border-zinc-200 p-4 dark:border-zinc-800">
      <div className="flex flex-wrap items-start justify-between gap-2">
        <div className="flex min-w-0 flex-col gap-0.5">
          <h3 className="break-words font-semibold">{documento.titulo}</h3>
          <p className="text-xs text-zinc-500">
            {TIPOS[documento.tipo]} · versión {documento.version}
            {documento.fuente && ` · ${documento.fuente}`}
          </p>
        </div>
        <span role="status" aria-label={`Estado: ${ESTADOS[documento.estado]}`} className={`rounded-full px-2 py-0.5 text-xs font-medium ${ESTILO_ESTADO[documento.estado]}`}>
          {ESTADOS[documento.estado]}
          {enProceso && "…"}
        </span>
      </div>

      <p className="text-sm text-zinc-600 dark:text-zinc-400">
        Cargado por {documento.cargadoPor} el {fechaHora(documento.fechaCarga)}.
        {documento.estado === "Indexado" &&
          ` ${documento.cantidadPaginas} ${documento.cantidadPaginas === 1 ? "página" : "páginas"}, ${documento.cantidadFragmentos} ${documento.cantidadFragmentos === 1 ? "fragmento" : "fragmentos"}.`}
      </p>
      {documento.errorProcesamiento && (
        <p className="rounded-md bg-red-50 px-3 py-2 text-sm text-red-700 dark:bg-red-950 dark:text-red-300">{documento.errorProcesamiento}</p>
      )}

      <div className="flex flex-wrap gap-2">
        {documento.cantidadFragmentos !== null && (
          <button type="button" className={botonChico} onClick={() => void alternarFragmentos()} aria-expanded={fragmentos !== null}>
            {fragmentos ? "Ocultar fragmentos" : "Ver fragmentos"}
          </button>
        )}
        <button type="button" className={botonChico} onClick={() => void onDescargar()}>
          Descargar PDF
        </button>
        {documento.estado !== "Retirado" && !enProceso && (
          <button type="button" className={botonChico} onClick={onNuevaVersion}>
            Subir nueva versión
          </button>
        )}
        {(documento.estado === "Indexado" || documento.estado === "Error") && (
          <button type="button" className={botonChico} onClick={() => void onReindexar()}>
            Reindexar
          </button>
        )}
        {documento.estado !== "Retirado" && !enProceso && !confirmarRetiro && (
          <button type="button" className={botonPeligroChico} onClick={() => setConfirmarRetiro(true)}>
            Retirar
          </button>
        )}
      </div>

      {confirmarRetiro && (
        <div className="flex flex-wrap items-center gap-2 rounded-md bg-red-50 p-3 text-sm text-red-900 dark:bg-red-950 dark:text-red-200">
          <span>El asistente deja de usarlo para responder. El archivo y su historial se conservan.</span>
          <button type="button" className={botonPeligroChico} onClick={() => void onRetirar().then(() => setConfirmarRetiro(false))}>
            Confirmar retiro
          </button>
          <button type="button" className={botonChico} onClick={() => setConfirmarRetiro(false)}>
            Cancelar
          </button>
        </div>
      )}

      {fragmentos && (
        <ol aria-label={`Fragmentos de ${documento.titulo}`} className="flex flex-col gap-2">
          {fragmentos.map((fragmento) => (
            <li key={fragmento.numeroFragmento} className="rounded-md bg-zinc-50 p-3 text-sm dark:bg-zinc-900">
              <p className="mb-1 text-xs text-zinc-500">
                #{fragmento.numeroFragmento} · {fragmento.paginaDesde === fragmento.paginaHasta ? `página ${fragmento.paginaDesde}` : `páginas ${fragmento.paginaDesde}–${fragmento.paginaHasta}`}
                {fragmento.seccion && ` · ${fragmento.seccion}`}
              </p>
              <p className="whitespace-pre-line break-words">{fragmento.contenido}</p>
            </li>
          ))}
          {documento.cantidadFragmentos !== null && documento.cantidadFragmentos > fragmentos.length && (
            <li className="text-xs text-zinc-500">Se muestran los primeros {fragmentos.length} de {documento.cantidadFragmentos}.</li>
          )}
        </ol>
      )}
    </article>
  );
}
