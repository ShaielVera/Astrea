"use client";

import Link from "next/link";
import { useCallback, useEffect, useRef, useState } from "react";
import { useAuth } from "@/features/auth/AuthProvider";
import { mensajeDeError } from "@/features/auth/mensajes-error";
import type { Pagina } from "@/features/academico/api";
import { Paginacion } from "@/features/mesas/Paginacion";
import { fechaHora } from "@/features/tramites/api";
import { ESTADOS, hallazgosApi, type EstadoObjeto, type HallazgoAdmin } from "@/features/hallazgos/api";
import { EstadoHallazgo, TarjetaHallazgo } from "@/features/hallazgos/TarjetaHallazgo";

const campo = "w-full min-w-0 rounded-md border border-zinc-300 bg-transparent px-3 py-2 font-normal dark:border-zinc-700 dark:bg-zinc-900";
const botonChico =
  "rounded-md border border-zinc-300 px-3 py-1.5 text-sm font-medium hover:bg-zinc-100 disabled:opacity-60 dark:border-zinc-700 dark:hover:bg-zinc-800";
const botonPrimarioChico = "rounded-md bg-indigo-600 px-3 py-1.5 text-sm font-medium text-white hover:bg-indigo-700 disabled:opacity-60";
const botonPeligroChico =
  "rounded-md border border-red-300 px-3 py-1.5 text-sm font-medium text-red-700 hover:bg-red-50 dark:border-red-900 dark:text-red-300 dark:hover:bg-red-950";

type Filtro = { estado?: EstadoObjeto; texto?: string };

export default function AdministracionHallazgos() {
  const { obtenerToken } = useAuth();
  const [filtro, setFiltro] = useState<Filtro>({ estado: "Publicado" });
  const [pagina, setPagina] = useState<Pagina<HallazgoAdmin> | null>(null);
  const [aviso, setAviso] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);
  const ultimaCarga = useRef(0);

  const cargar = useCallback(
    async (criterio: Filtro, page: number) => {
      const numero = ++ultimaCarga.current;
      try {
        const resultado = await hallazgosApi.buscarAdmin(await obtenerToken(), { ...criterio, page });
        if (numero === ultimaCarga.current) setPagina(resultado);
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
      .then((token) => hallazgosApi.buscarAdmin(token, { estado: "Publicado" }))
      .then((resultado) => vigente && numero === ultimaCarga.current && setPagina(resultado))
      .catch((e) => vigente && setError(mensajeDeError(e)));
    return () => {
      vigente = false;
    };
  }, [obtenerToken]);

  async function cerrar(accion: (token: string) => Promise<HallazgoAdmin>, mensaje: (h: HallazgoAdmin) => string) {
    setError(null);
    setAviso(null);
    try {
      const actualizado = await accion(await obtenerToken());
      setAviso(mensaje(actualizado));
      await cargar(filtro, pagina?.page ?? 1);
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
        <h1 className="text-3xl font-semibold tracking-tight">Hallazgos</h1>
        <p className="text-zinc-600 dark:text-zinc-400">
          Antes de entregar, pedí el DNI y que la persona demuestre que el objeto es suyo: que describa los detalles privados o muestre una foto, la factura o el ticket de compra. Los hallazgos entregados o dados de baja se borran a los 30 días, con su foto y los datos de quien lo retiró.
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

      <form
        aria-label="Filtrar hallazgos"
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
            onChange={(e) => {
              // El estado se aplica en el momento (la búsqueda por texto, con "Filtrar").
              const nuevo = { ...filtro, estado: (e.target.value || undefined) as EstadoObjeto | undefined };
              setFiltro(nuevo);
              void cargar(nuevo, 1);
            }}
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
          Qué buscás
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

      {pagina?.totalItems === 0 && <p className="text-sm text-zinc-500">No hay hallazgos con esos filtros.</p>}
      <div className="grid grid-cols-1 gap-4 md:grid-cols-2">
        {pagina?.items.map((hallazgo) => (
          <TarjetaHallazgo
            key={`${hallazgo.id}-${hallazgo.estado}`}
            hallazgo={hallazgo}
            extra={
              <DetalleAdministracion
                hallazgo={hallazgo}
                onEntregar={(datos) =>
                  cerrar((token) => hallazgosApi.entregar(token, hallazgo.id, datos), (h) => `"${h.tituloPublico}" quedó entregado a ${h.entrega?.nombreRetirante}.`)
                }
                onDarDeBaja={(motivo) => cerrar((token) => hallazgosApi.darDeBaja(token, hallazgo.id, motivo), (h) => `"${h.tituloPublico}" fue dado de baja.`)}
              />
            }
          />
        ))}
      </div>
      {pagina && <Paginacion pagina={pagina} onCambiar={(page) => void cargar(filtro, page)} />}
    </main>
  );
}

type DatosEntrega = { nombreRetirante: string; dniRetirante: string; identidadVerificada: boolean; observacionInterna: string | null };

function DetalleAdministracion({
  hallazgo,
  onEntregar,
  onDarDeBaja,
}: {
  hallazgo: HallazgoAdmin;
  onEntregar: (datos: DatosEntrega) => Promise<void>;
  onDarDeBaja: (motivo: string) => Promise<void>;
}) {
  const [accion, setAccion] = useState<"entregar" | "baja" | null>(null);
  const publicador = [hallazgo.publicadoPor.apellido, hallazgo.publicadoPor.nombre].filter(Boolean).join(", ") || hallazgo.publicadoPor.email;

  return (
    <div className="flex flex-col gap-3 border-t border-zinc-200 pt-3 text-sm dark:border-zinc-800">
      <div className="flex flex-wrap items-center gap-2">
        <EstadoHallazgo estado={hallazgo.estado} />
        <span className="text-xs text-zinc-500">
          Publicado por {publicador} el {fechaHora(hallazgo.fechaPublicacion)}
        </span>
      </div>
      <p>
        <span className="font-medium">Detalles privados:</span> {hallazgo.descripcionPrivada}
      </p>

      {hallazgo.entrega && (
        <p className="rounded-md bg-emerald-50 px-3 py-2 dark:bg-emerald-950/40">
          Entregado a <span className="font-medium">{hallazgo.entrega.nombreRetirante}</span> (DNI {hallazgo.entrega.dniRetirante}) el{" "}
          {fechaHora(hallazgo.entrega.fechaEntrega)}. Verificó: {hallazgo.entrega.verificadoPor}.
          {hallazgo.entrega.observacionInterna && ` ${hallazgo.entrega.observacionInterna}`}
        </p>
      )}
      {hallazgo.motivoBaja && (
        <p className="rounded-md bg-zinc-100 px-3 py-2 dark:bg-zinc-800">
          Dado de baja por {hallazgo.dadoDeBajaPor}: {hallazgo.motivoBaja}
        </p>
      )}

      {hallazgo.estado === "Publicado" && accion === null && (
        <div className="flex flex-wrap gap-2">
          <button type="button" className={botonPrimarioChico} onClick={() => setAccion("entregar")}>
            Entregar
          </button>
          <button type="button" className={botonPeligroChico} onClick={() => setAccion("baja")}>
            Dar de baja
          </button>
        </div>
      )}
      {accion === "entregar" && <FormularioEntrega titulo={hallazgo.tituloPublico} onEntregar={onEntregar} onVolver={() => setAccion(null)} />}
      {accion === "baja" && <FormularioBaja titulo={hallazgo.tituloPublico} onDarDeBaja={onDarDeBaja} onVolver={() => setAccion(null)} />}
    </div>
  );
}

function FormularioEntrega({ titulo, onEntregar, onVolver }: { titulo: string; onEntregar: (datos: DatosEntrega) => Promise<void>; onVolver: () => void }) {
  const [nombre, setNombre] = useState("");
  const [dni, setDni] = useState("");
  const [verificado, setVerificado] = useState(false);
  const listoParaEntregar = Boolean(nombre.trim() && dni.trim() && verificado);
  const [observacion, setObservacion] = useState("");
  const [enviando, setEnviando] = useState(false);

  return (
    <form
      aria-label={`Entregar ${titulo}`}
      className="flex flex-col gap-2 rounded-lg bg-zinc-50 p-3 dark:bg-zinc-900"
      onSubmit={async (e) => {
        e.preventDefault();
        setEnviando(true);
        try {
          await onEntregar({ nombreRetirante: nombre, dniRetirante: dni, identidadVerificada: verificado, observacionInterna: observacion.trim() || null });
        } finally {
          setEnviando(false);
        }
      }}
    >
      <label className="flex min-w-0 flex-col gap-1 font-medium">
        Nombre y apellido de quien lo retira
        <input value={nombre} onChange={(e) => setNombre(e.target.value)} required maxLength={120} className={campo} />
      </label>
      <label className="flex min-w-0 flex-col gap-1 font-medium">
        DNI de quien lo retira
        <input value={dni} onChange={(e) => setDni(e.target.value)} required inputMode="numeric" maxLength={12} className={campo} />
      </label>
      <label className="flex min-w-0 flex-col gap-1 font-medium">
        Observación interna (opcional)
        <input value={observacion} onChange={(e) => setObservacion(e.target.value)} maxLength={500} placeholder="Ej.: describió la calcomanía y mostró una foto con el termo" className={campo} />
      </label>
      <label className="flex items-start gap-2">
        <input type="checkbox" checked={verificado} onChange={(e) => setVerificado(e.target.checked)} required className="mt-1" />
        <span>Verifiqué en persona su identidad con el DNI y que demostró que el objeto le pertenece (detalles privados, foto, factura o ticket de compra).</span>
      </label>
      {/* El botón se habilita recién con los datos de quien retira y la verificación tildada: así queda claro qué falta. */}
      {!listoParaEntregar && (
        <p className="text-xs text-amber-800 dark:text-amber-300">
          {!nombre.trim() || !dni.trim()
            ? "Completá el nombre y el DNI de quien lo retira."
            : "Tildá la verificación de identidad para poder confirmar la entrega."}
        </p>
      )}
      <div className="flex gap-2">
        <button type="submit" className={botonPrimarioChico} disabled={enviando || !listoParaEntregar}>
          Confirmar entrega
        </button>
        <button type="button" className={botonChico} onClick={onVolver}>
          Volver
        </button>
      </div>
    </form>
  );
}

function FormularioBaja({ titulo, onDarDeBaja, onVolver }: { titulo: string; onDarDeBaja: (motivo: string) => Promise<void>; onVolver: () => void }) {
  const [motivo, setMotivo] = useState("");

  return (
    <form
      aria-label={`Dar de baja ${titulo}`}
      className="flex flex-col gap-2 rounded-lg bg-zinc-50 p-3 dark:bg-zinc-900"
      onSubmit={(e) => {
        e.preventDefault();
        void onDarDeBaja(motivo);
      }}
    >
      <label className="flex min-w-0 flex-col gap-1 font-medium">
        Motivo de la baja
        <input value={motivo} onChange={(e) => setMotivo(e.target.value)} required maxLength={300} placeholder="Ej.: publicación duplicada" className={campo} />
      </label>
      <div className="flex gap-2">
        <button type="submit" className={botonPeligroChico}>
          Confirmar baja
        </button>
        <button type="button" className={botonChico} onClick={onVolver}>
          Volver
        </button>
      </div>
    </form>
  );
}
