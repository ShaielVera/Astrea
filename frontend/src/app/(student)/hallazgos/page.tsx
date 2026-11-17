"use client";

import { useCallback, useEffect, useRef, useState, type FormEvent } from "react";
import { useAuth } from "@/features/auth/AuthProvider";
import { mensajeDeError } from "@/features/auth/mensajes-error";
import type { Pagina } from "@/features/academico/api";
import { Paginacion } from "@/features/mesas/Paginacion";
import {
  CATEGORIAS,
  FOTO_MAXIMO_BYTES,
  FOTO_TIPOS,
  FRANJAS,
  PUNTOS,
  hallazgosApi,
  type CategoriaObjeto,
  type FranjaHoraria,
  type HallazgoPublico,
  type MiHallazgo,
  type PuntoResguardo,
} from "@/features/hallazgos/api";
import { EstadoHallazgo, TarjetaHallazgo } from "@/features/hallazgos/TarjetaHallazgo";

const campo = "w-full min-w-0 rounded-md border border-zinc-300 bg-transparent px-3 py-2 font-normal dark:border-zinc-700 dark:bg-zinc-900";
const botonPrimario = "rounded-md bg-indigo-600 px-4 py-2 font-medium text-white hover:bg-indigo-700 disabled:opacity-60";
const botonSecundario =
  "rounded-md border border-zinc-300 px-4 py-2 font-medium hover:bg-zinc-100 dark:border-zinc-700 dark:hover:bg-zinc-800";

type Filtro = { texto?: string; categoria?: CategoriaObjeto; punto?: PuntoResguardo };

/** Hoy en el instituto (AAAA-MM-DD). */
const hoy = () => new Intl.DateTimeFormat("en-CA", { timeZone: "America/Argentina/Buenos_Aires" }).format(new Date());

export default function HallazgosPage() {
  const { obtenerToken } = useAuth();
  const [filtro, setFiltro] = useState<Filtro>({});
  const [aplicado, setAplicado] = useState<Filtro>({});
  const [pagina, setPagina] = useState<Pagina<HallazgoPublico> | null>(null);
  const [mios, setMios] = useState<MiHallazgo[]>([]);
  const [publicando, setPublicando] = useState(false);
  const [aviso, setAviso] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);
  const ultimaBusqueda = useRef(0);

  const buscar = useCallback(
    async (criterio: Filtro, page: number) => {
      const numero = ++ultimaBusqueda.current;
      setError(null);
      try {
        const resultado = await hallazgosApi.buscar(await obtenerToken(), { ...criterio, page });
        if (numero !== ultimaBusqueda.current) return;
        setPagina(resultado);
        setAplicado(criterio);
      } catch (e) {
        if (numero === ultimaBusqueda.current) setError(mensajeDeError(e));
      }
    },
    [obtenerToken],
  );

  useEffect(() => {
    let vigente = true;
    const numero = ++ultimaBusqueda.current;
    obtenerToken()
      .then((token) => Promise.all([hallazgosApi.buscar(token, {}), hallazgosApi.mios(token)]))
      .then(([resultado, propios]) => {
        if (!vigente) return;
        if (numero === ultimaBusqueda.current) setPagina(resultado);
        setMios(propios.items);
      })
      .catch((e) => vigente && setError(mensajeDeError(e)));
    return () => {
      vigente = false;
    };
  }, [obtenerToken]);

  async function publicado(hallazgo: MiHallazgo, errorFoto: string | null) {
    setPublicando(false);
    setAviso(`Publicaste "${hallazgo.tituloPublico}". Llevalo a ${PUNTOS[hallazgo.puntoResguardo]} para que su dueño lo pueda retirar.`);
    setError(errorFoto ? `Se publicó, pero no se pudo subir la foto: ${errorFoto} No va a aparecer en la búsqueda hasta que la subas desde "Lo que publicaste".` : null);
    await actualizar();
  }

  /** Recarga lo propio y la búsqueda (después de publicar o de subir una foto que faltaba). */
  async function actualizar() {
    const token = await obtenerToken();
    setMios((await hallazgosApi.mios(token)).items);
    await buscar(aplicado, 1);
  }

  return (
    <main className="mx-auto flex w-full max-w-5xl flex-1 flex-col gap-6 px-4 py-10 sm:px-6">
      <div className="flex flex-wrap items-end justify-between gap-3">
        <div className="flex flex-col gap-1">
          <h1 className="text-3xl font-semibold tracking-tight">Hallazgos</h1>
          <p className="text-zinc-600 dark:text-zinc-400">Objetos encontrados en el instituto. Si algo es tuyo, andá a retirarlo al lugar indicado.</p>
        </div>
        {!publicando && (
          <button type="button" className={botonPrimario} onClick={() => setPublicando(true)}>
            Encontré algo
          </button>
        )}
      </div>

      <div role="note" aria-label="Cómo retirar un objeto" className="rounded-md border border-amber-200 bg-amber-50 px-3 py-2 text-sm text-amber-900 dark:border-amber-900 dark:bg-amber-950 dark:text-amber-200">
        <span className="font-medium">¿Es tuyo?</span> Para retirarlo vas a tener que mostrar tu DNI y demostrar que te pertenece: describir detalles que no
        figuran en la publicación, o mostrar una foto tuya con el objeto, la factura o el ticket de compra.
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

      {publicando && <FormularioPublicacion onPublicado={publicado} onCancelar={() => setPublicando(false)} />}

      <section aria-labelledby="buscar-titulo" className="flex flex-col gap-4">
        <h2 id="buscar-titulo" className="text-lg font-semibold">
          Buscar
        </h2>
        <form
          aria-label="Buscar hallazgos"
          className="grid grid-cols-1 items-end gap-3 sm:grid-cols-[minmax(0,2fr)_minmax(0,1fr)_minmax(0,1fr)_auto]"
          onSubmit={(e) => {
            e.preventDefault();
            void buscar(filtro, 1);
          }}
        >
          <label className="flex min-w-0 flex-col gap-1 text-sm font-medium">
            Qué buscás
            <input
              type="search"
              value={filtro.texto ?? ""}
              onChange={(e) => setFiltro((f) => ({ ...f, texto: e.target.value || undefined }))}
              maxLength={100}
              placeholder="Ej.: termo, llaves, aula 3"
              className={campo}
            />
          </label>
          <label className="flex min-w-0 flex-col gap-1 text-sm font-medium">
            Categoría
            <select
              value={filtro.categoria ?? ""}
              onChange={(e) => setFiltro((f) => ({ ...f, categoria: (e.target.value || undefined) as CategoriaObjeto | undefined }))}
              className={campo}
            >
              <option value="">Todas</option>
              {Object.entries(CATEGORIAS).map(([valor, etiqueta]) => (
                <option key={valor} value={valor}>
                  {etiqueta.split(" (")[0]}
                </option>
              ))}
            </select>
          </label>
          <label className="flex min-w-0 flex-col gap-1 text-sm font-medium">
            Dónde está
            <select
              value={filtro.punto ?? ""}
              onChange={(e) => setFiltro((f) => ({ ...f, punto: (e.target.value || undefined) as PuntoResguardo | undefined }))}
              className={campo}
            >
              <option value="">Cualquier lugar</option>
              {Object.entries(PUNTOS).map(([valor, etiqueta]) => (
                <option key={valor} value={valor}>
                  {etiqueta}
                </option>
              ))}
            </select>
          </label>
          <button type="submit" className={botonSecundario}>
            Buscar
          </button>
        </form>

        {pagina && (
          <p role="status" aria-live="polite" className="text-sm text-zinc-500">
            {pagina.totalItems === 0 ? "No hay objetos publicados con esos filtros." : `${pagina.totalItems} ${pagina.totalItems === 1 ? "objeto" : "objetos"}.`}
          </p>
        )}
        <div className="grid grid-cols-1 gap-4 sm:grid-cols-2 lg:grid-cols-3">
          {pagina?.items.map((hallazgo) => <TarjetaHallazgo key={hallazgo.id} hallazgo={hallazgo} />)}
        </div>
        {pagina && <Paginacion pagina={pagina} onCambiar={(page) => void buscar(aplicado, page)} />}
      </section>

      {mios.length > 0 && (
        <section aria-labelledby="mios-titulo" className="flex flex-col gap-3">
          <h2 id="mios-titulo" className="text-lg font-semibold">
            Lo que publicaste
          </h2>
          <div className="grid grid-cols-1 gap-4 sm:grid-cols-2 lg:grid-cols-3">
            {mios.map((hallazgo) => (
              <TarjetaHallazgo
                key={hallazgo.id}
                hallazgo={hallazgo}
                extra={
                  <div className="flex flex-col gap-1 text-sm">
                    <EstadoHallazgo estado={hallazgo.estado} />
                    <p className="text-zinc-600 dark:text-zinc-400">
                      <span className="font-medium">Detalles privados:</span> {hallazgo.descripcionPrivada}
                    </p>
                    {hallazgo.estado === "Publicado" && !hallazgo.tieneFoto && <FotoFaltante hallazgo={hallazgo} onSubida={actualizar} />}
                  </div>
                }
              />
            ))}
          </div>
        </section>
      )}
    </main>
  );
}

/** Control rápido en el navegador; el servidor vuelve a validar el contenido real. */
function problemaDeFoto(archivo: File): string | null {
  if (!FOTO_TIPOS.includes(archivo.type)) return "La foto tiene que ser JPEG, PNG o WebP.";
  if (archivo.size > FOTO_MAXIMO_BYTES) return "La foto no puede superar los 5 MB.";
  return null;
}

/** Para un hallazgo propio que quedó sin foto (falló la subida): hasta que la tenga no aparece en la búsqueda. */
function FotoFaltante({ hallazgo, onSubida }: { hallazgo: MiHallazgo; onSubida: () => Promise<void> }) {
  const { obtenerToken } = useAuth();
  const [subiendo, setSubiendo] = useState(false);
  const [error, setError] = useState<string | null>(null);

  async function subir(archivo: File | undefined) {
    if (!archivo) return;
    const problema = problemaDeFoto(archivo);
    setError(problema);
    if (problema) return;
    setSubiendo(true);
    try {
      await hallazgosApi.subirFoto(await obtenerToken(), hallazgo.id, archivo);
      await onSubida();
    } catch (e) {
      setError(mensajeDeError(e));
    } finally {
      setSubiendo(false);
    }
  }

  return (
    <div className="flex flex-col gap-1 rounded-md bg-amber-50 p-2 text-amber-900 dark:bg-amber-950 dark:text-amber-200">
      <p>Falta la foto: no aparece en la búsqueda hasta que la subas.</p>
      {error && <p role="alert">{error}</p>}
      <label className="flex min-w-0 flex-col gap-1 font-medium">
        {subiendo ? "Subiendo…" : "Subir foto"}
        <input
          type="file"
          accept={FOTO_TIPOS.join(",")}
          disabled={subiendo}
          onChange={(e) => void subir(e.target.files?.[0])}
          className="text-sm font-normal file:mr-3 file:rounded-md file:border-0 file:bg-white file:px-3 file:py-2 dark:file:bg-zinc-800"
        />
      </label>
    </div>
  );
}

function FormularioPublicacion({
  onPublicado,
  onCancelar,
}: {
  onPublicado: (hallazgo: MiHallazgo, errorFoto: string | null) => Promise<void>;
  onCancelar: () => void;
}) {
  const { obtenerToken } = useAuth();
  const [titulo, setTitulo] = useState("");
  const [categoria, setCategoria] = useState<CategoriaObjeto>("Otro");
  const [sector, setSector] = useState("");
  const [fecha, setFecha] = useState(hoy());
  const [franja, setFranja] = useState<FranjaHoraria>("Manana");
  const [punto, setPunto] = useState<PuntoResguardo>("Direccion");
  const [descripcion, setDescripcion] = useState("");
  const [foto, setFoto] = useState<{ archivo: File; vistaPrevia: string } | null>(null);
  const [enviando, setEnviando] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const campoFoto = useRef<HTMLInputElement>(null);

  // La miniatura (blob:) se libera al cambiar de foto o al salir del formulario.
  useEffect(() => () => {
    if (foto) URL.revokeObjectURL(foto.vistaPrevia);
  }, [foto]);

  function elegirFoto(archivo: File | undefined) {
    const problema = archivo ? problemaDeFoto(archivo) : null;
    setError(problema);
    setFoto(archivo && !problema ? { archivo, vistaPrevia: URL.createObjectURL(archivo) } : null);
    if (problema && campoFoto.current) campoFoto.current.value = "";
  }

  function quitarFoto() {
    setFoto(null);
    if (campoFoto.current) campoFoto.current.value = "";
    campoFoto.current?.focus();
  }

  async function enviar(evento: FormEvent<HTMLFormElement>) {
    evento.preventDefault();
    if (!foto) {
      setError("Agregá una foto del objeto: sin foto el dueño no lo puede reconocer.");
      return;
    }
    setEnviando(true);
    setError(null);
    try {
      const token = await obtenerToken();
      let hallazgo = await hallazgosApi.publicar(token, {
        tituloPublico: titulo,
        descripcionPrivada: descripcion,
        categoria,
        sectorEncontrado: sector,
        franjaHoraria: franja,
        puntoResguardo: punto,
        fechaHallazgo: fecha,
      });
      // Ya quedó publicado: si la foto falla, no se reenvía el formulario (duplicaría el hallazgo); se sube desde "Lo que publicaste".
      let errorFoto: string | null = null;
      try {
        hallazgo = await hallazgosApi.subirFoto(token, hallazgo.id, foto.archivo);
      } catch (e) {
        errorFoto = mensajeDeError(e);
      }
      await onPublicado(hallazgo, errorFoto);
    } catch (e) {
      setError(mensajeDeError(e));
    } finally {
      setEnviando(false);
    }
  }

  return (
    <form onSubmit={enviar} aria-label="Publicar un hallazgo" className="flex flex-col gap-4 rounded-xl border bg-superficie border-indigo-200 p-4 dark:border-indigo-900">
      <h2 className="text-lg font-semibold">Publicar un hallazgo</h2>
      {error && (
        <p role="alert" className="rounded-md bg-red-50 px-3 py-2 text-sm text-red-700 dark:bg-red-950 dark:text-red-300">
          {error}
        </p>
      )}
      <div className="grid grid-cols-1 gap-3 sm:grid-cols-2">
        <label className="flex min-w-0 flex-col gap-1 text-sm font-medium">
          Qué encontraste
          <input value={titulo} onChange={(e) => setTitulo(e.target.value)} required maxLength={80} placeholder="Ej.: Termo negro" className={campo} />
        </label>
        <label className="flex min-w-0 flex-col gap-1 text-sm font-medium">
          Categoría
          <select value={categoria} onChange={(e) => setCategoria(e.target.value as CategoriaObjeto)} className={campo}>
            {Object.entries(CATEGORIAS).map(([valor, etiqueta]) => (
              <option key={valor} value={valor}>
                {etiqueta}
              </option>
            ))}
          </select>
        </label>
        <label className="flex min-w-0 flex-col gap-1 text-sm font-medium">
          Dónde lo encontraste
          <input value={sector} onChange={(e) => setSector(e.target.value)} required maxLength={80} placeholder="Ej.: Aula 3, planta baja" className={campo} />
        </label>
        <div className="grid grid-cols-2 gap-3">
          <label className="flex min-w-0 flex-col gap-1 text-sm font-medium">
            Fecha
            <input type="date" value={fecha} max={hoy()} onChange={(e) => setFecha(e.target.value)} required className={campo} />
          </label>
          <label className="flex min-w-0 flex-col gap-1 text-sm font-medium">
            Turno
            <select value={franja} onChange={(e) => setFranja(e.target.value as FranjaHoraria)} className={campo}>
              {Object.entries(FRANJAS).map(([valor, etiqueta]) => (
                <option key={valor} value={valor}>
                  {etiqueta}
                </option>
              ))}
            </select>
          </label>
        </div>
        <label className="flex min-w-0 flex-col gap-1 text-sm font-medium">
          Dónde lo dejás
          <select value={punto} onChange={(e) => setPunto(e.target.value as PuntoResguardo)} aria-describedby="ayuda-punto" className={campo}>
            {Object.entries(PUNTOS).map(([valor, etiqueta]) => (
              <option key={valor} value={valor}>
                {etiqueta}
              </option>
            ))}
          </select>
          <span id="ayuda-punto" className="text-xs font-normal text-zinc-500">
            Herramientas, equipos u objetos grandes van al Depósito.
          </span>
        </label>
        <label className="flex min-w-0 flex-col gap-1 text-sm font-medium">
          Foto
          <input
            ref={campoFoto}
            type="file"
            required
            accept={FOTO_TIPOS.join(",")}
            onChange={(e) => elegirFoto(e.target.files?.[0])}
            aria-describedby="ayuda-foto"
            className="text-sm font-normal file:mr-3 file:rounded-md file:border-0 file:bg-zinc-100 file:px-3 file:py-2 dark:file:bg-zinc-800"
          />
          <span id="ayuda-foto" className="text-xs font-normal text-zinc-500">
            Obligatoria: es lo que le permite al dueño reconocerlo (por ejemplo, entre dos cartucheras parecidas). JPEG, PNG o WebP, hasta 5 MB.
            Que no se vean nombres, documentos ni las marcas que pusiste en los detalles privados.
          </span>
        </label>
        {foto && (
          <div className="flex items-center gap-3 sm:col-span-2">
            {/* eslint-disable-next-line @next/next/no-img-element -- vista previa local (blob:), no pasa por el optimizador */}
            <img src={foto.vistaPrevia} alt="Vista previa de la foto elegida" className="size-20 rounded-lg border border-zinc-200 object-cover dark:border-zinc-700" />
            <div className="flex flex-col items-start gap-1 text-sm">
              <span className="max-w-xs truncate text-zinc-600 dark:text-zinc-400">{foto.archivo.name}</span>
              <button type="button" onClick={quitarFoto} className={botonSecundario}>
                Quitar foto
              </button>
            </div>
          </div>
        )}
      </div>
      <label className="flex min-w-0 flex-col gap-1 text-sm font-medium">
        Detalles privados
        <textarea value={descripcion} onChange={(e) => setDescripcion(e.target.value)} required maxLength={500} rows={3} aria-describedby="ayuda-privada" className={campo} />
        <span id="ayuda-privada" className="text-xs font-normal text-zinc-500">
          Marcas, contenido, inscripciones… No se publican: solo los ve la Secretaría para comprobar que quien lo reclama es el dueño.
        </span>
      </label>
      <div className="flex flex-wrap gap-3">
        <button type="submit" className={botonPrimario} disabled={enviando}>
          {enviando ? "Publicando…" : "Publicar"}
        </button>
        <button type="button" className={botonSecundario} onClick={onCancelar}>
          Cancelar
        </button>
      </div>
    </form>
  );
}
