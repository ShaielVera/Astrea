"use client";

import Link from "next/link";
import { useCallback, useEffect, useRef, useState, type FormEvent } from "react";
import { useAuth } from "@/features/auth/AuthProvider";
import { mensajeDeError } from "@/features/auth/mensajes-error";
import type { Pagina } from "@/features/academico/api";
import { Paginacion } from "@/features/mesas/Paginacion";
import { fechaHora, tramitesApi, type EstadoTramite, type TramiteDetalle, type TramiteResumen } from "@/features/tramites/api";
import { DetalleTramite, EstadoBadge } from "@/features/tramites/DetalleTramite";
import { FormularioAnulacion, FormularioDatosOficiales } from "@/features/tramites/FormulariosAdministracion";

const campo = "w-full min-w-0 rounded-md border border-zinc-300 bg-transparent px-3 py-2 font-normal dark:border-zinc-700 dark:bg-zinc-900";
const botonChico =
  "rounded-md border border-zinc-300 px-3 py-1.5 text-sm font-medium hover:bg-zinc-100 disabled:opacity-60 dark:border-zinc-700 dark:hover:bg-zinc-800";
const botonPrimarioChico = "rounded-md bg-indigo-600 px-3 py-1.5 text-sm font-medium text-white hover:bg-indigo-700 disabled:opacity-60";
const botonPeligroChico =
  "rounded-md border border-red-300 px-3 py-1.5 text-sm font-medium text-red-700 hover:bg-red-50 disabled:opacity-60 dark:border-red-900 dark:text-red-300 dark:hover:bg-red-950";

/** Opciones del filtro de estado: "abiertos" agrupa lo que todavía requiere trabajo. */
const FILTROS: Record<string, { etiqueta: string; estados: EstadoTramite[] }> = {
  abiertos: { etiqueta: "Abiertos (pendientes y en proceso)", estados: ["Pendiente", "EnProceso"] },
  Pendiente: { etiqueta: "Pendientes", estados: ["Pendiente"] },
  EnProceso: { etiqueta: "En proceso", estados: ["EnProceso"] },
  Listo: { etiqueta: "Listos", estados: ["Listo"] },
  Rechazado: { etiqueta: "Rechazados", estados: ["Rechazado"] },
  todos: { etiqueta: "Todos", estados: [] },
};

export default function BandejaTramites() {
  const { obtenerToken } = useAuth();
  const [filtro, setFiltro] = useState("abiertos");
  const [texto, setTexto] = useState("");
  const [pagina, setPagina] = useState<Pagina<TramiteResumen> | null>(null);
  const [seleccionado, setSeleccionado] = useState<TramiteDetalle | null>(null);
  const [aviso, setAviso] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);
  const ultimaCarga = useRef(0);

  const cargar = useCallback(
    async (clave: string, busqueda: string, page: number) => {
      const numero = ++ultimaCarga.current;
      try {
        const resultado = await tramitesApi.bandeja(await obtenerToken(), { estados: FILTROS[clave].estados, texto: busqueda || undefined, page });
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
      .then((token) => tramitesApi.bandeja(token, { estados: FILTROS.abiertos.estados }))
      .then((resultado) => vigente && numero === ultimaCarga.current && setPagina(resultado))
      .catch((e) => vigente && setError(mensajeDeError(e)));
    return () => {
      vigente = false;
    };
  }, [obtenerToken]);

  function buscar(evento: FormEvent<HTMLFormElement>) {
    evento.preventDefault();
    void cargar(filtro, texto.trim(), 1);
  }

  async function abrir(id: string) {
    setError(null);
    setAviso(null);
    try {
      setSeleccionado(await tramitesApi.detalleAdmin(await obtenerToken(), id));
    } catch (e) {
      setError(mensajeDeError(e));
    }
  }

  async function descargar(id: string) {
    setError(null);
    try {
      await tramitesApi.descargarAdmin(await obtenerToken(), id);
    } catch (e) {
      setError(mensajeDeError(e));
    }
  }

  /** Aplica un cambio de estado y refresca el detalle y la bandeja. */
  async function cambiar(accion: (token: string) => Promise<TramiteDetalle>, mensaje: (t: TramiteDetalle) => string) {
    setError(null);
    setAviso(null);
    try {
      const actualizado = await accion(await obtenerToken());
      setSeleccionado(actualizado);
      setAviso(mensaje(actualizado));
      await cargar(filtro, texto.trim(), pagina?.page ?? 1);
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
        <h1 className="text-3xl font-semibold tracking-tight">Bandeja de trámites</h1>
        <p className="text-zinc-600 dark:text-zinc-400">Los pedidos más antiguos aparecen primero.</p>
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

      <form onSubmit={buscar} aria-label="Filtrar trámites" className="grid grid-cols-1 items-end gap-3 sm:grid-cols-[minmax(0,1fr)_minmax(0,1fr)_auto]">
        <label className="flex min-w-0 flex-col gap-1 text-sm font-medium">
          Estado
          <select value={filtro} onChange={(e) => setFiltro(e.target.value)} className={campo}>
            {Object.entries(FILTROS).map(([clave, { etiqueta }]) => (
              <option key={clave} value={clave}>
                {etiqueta}
              </option>
            ))}
          </select>
        </label>
        <label className="flex min-w-0 flex-col gap-1 text-sm font-medium">
          Número, email o apellido
          <input type="search" value={texto} onChange={(e) => setTexto(e.target.value)} maxLength={100} placeholder="Ej.: T-000012" className={campo} />
        </label>
        <button type="submit" className={botonChico}>
          Buscar
        </button>
      </form>

      <div className="grid grid-cols-1 gap-6 lg:grid-cols-[minmax(0,2fr)_minmax(0,3fr)]">
        <section aria-label="Trámites" className="flex min-w-0 flex-col gap-3">
          {pagina?.totalItems === 0 && <p className="text-sm text-zinc-500">No hay trámites con esos filtros.</p>}
          <ul className="flex flex-col gap-2">
            {pagina?.items.map((tramite) => (
              <li key={tramite.id}>
                <button
                  type="button"
                  onClick={() => void abrir(tramite.id)}
                  aria-current={seleccionado?.id === tramite.id}
                  className="flex w-full flex-col gap-1 rounded-lg border border-zinc-200 px-3 py-2 text-left hover:border-indigo-400 aria-[current=true]:border-indigo-500 dark:border-zinc-800"
                >
                  <span className="flex flex-wrap items-center justify-between gap-2">
                    <span className="font-medium">
                      {tramite.numero} · {tramite.tipoNombre}
                    </span>
                    <EstadoBadge estado={tramite.estado} />
                  </span>
                  <span className="break-all text-xs text-zinc-500">
                    {[tramite.solicitanteApellido, tramite.solicitanteNombre].filter(Boolean).join(", ") || tramite.solicitanteEmail} ·{" "}
                    {fechaHora(tramite.fechaSolicitud)}
                  </span>
                </button>
              </li>
            ))}
          </ul>
          {pagina && <Paginacion pagina={pagina} onCambiar={(page) => void cargar(filtro, texto.trim(), page)} />}
        </section>

        <div className="min-w-0">
          {seleccionado ? (
            <DetalleTramite
              key={`${seleccionado.id}-${seleccionado.estado}-${seleccionado.documento?.estado ?? "sin-documento"}`}
              tramite={seleccionado}
              vistaAdministracion
              onDescargar={() => void descargar(seleccionado.id)}
              accionesDocumento={
                seleccionado.documento?.estado === "Vigente" && (
                  <FormularioAnulacion
                    onAnular={(motivo) =>
                      cambiar((token) => tramitesApi.anularDocumento(token, seleccionado.id, motivo), (t) => `El documento de ${t.numero} quedó anulado.`)
                    }
                  />
                )
              }
              acciones={
                <>
                  {seleccionado.emiteDocumento && (seleccionado.estado === "Pendiente" || seleccionado.estado === "EnProceso") && (
                    <FormularioDatosOficiales
                      key={seleccionado.solicitante.id + (seleccionado.solicitante.dni ?? "")}
                      persona={seleccionado.solicitante}
                      onGuardar={async (datos) => {
                        setError(null);
                        try {
                          const token = await obtenerToken();
                          await tramitesApi.datosOficiales(token, seleccionado.solicitante.id, datos);
                          setSeleccionado(await tramitesApi.detalleAdmin(token, seleccionado.id));
                          setAviso("Datos oficiales guardados.");
                        } catch (e) {
                          setError(mensajeDeError(e));
                        }
                      }}
                    />
                  )}
                  <AccionesTramite
                    tramite={seleccionado}
                    onTomar={() => cambiar((token) => tramitesApi.tomar(token, seleccionado.id), (t) => `${t.numero} quedó en proceso.`)}
                    onAprobar={(comentario) =>
                      cambiar(
                        (token) => tramitesApi.aprobar(token, seleccionado.id, comentario),
                        (t) => (t.documento ? `${t.numero} quedó listo y se emitió el PDF.` : `${t.numero} quedó listo.`),
                      )
                    }
                    onRechazar={(motivo) => cambiar((token) => tramitesApi.rechazar(token, seleccionado.id, motivo), (t) => `${t.numero} fue rechazado.`)}
                  />
                </>
              }
            />
          ) : (
            <p className="text-sm text-zinc-500">Elegí un trámite de la lista para revisarlo.</p>
          )}
        </div>
      </div>
    </main>
  );
}

function AccionesTramite({
  tramite,
  onTomar,
  onAprobar,
  onRechazar,
}: {
  tramite: TramiteDetalle;
  onTomar: () => Promise<void>;
  onAprobar: (comentario: string | null) => Promise<void>;
  onRechazar: (motivo: string) => Promise<void>;
}) {
  const [comentario, setComentario] = useState("");
  const [motivo, setMotivo] = useState("");
  const [ocupado, setOcupado] = useState(false);

  if (tramite.estado === "Listo" || tramite.estado === "Rechazado") return null;

  async function ejecutar(accion: () => Promise<void>) {
    setOcupado(true);
    try {
      await accion();
    } finally {
      setOcupado(false);
    }
  }

  return (
    <div className="flex flex-col gap-3 rounded-lg bg-zinc-50 p-3 dark:bg-zinc-900">
      {tramite.estado === "Pendiente" && (
        <div>
          <button type="button" className={botonChico} disabled={ocupado} onClick={() => void ejecutar(onTomar)}>
            Tomar trámite
          </button>
        </div>
      )}
      <form
        aria-label="Aprobar trámite"
        className="flex flex-col gap-2"
        onSubmit={(e) => {
          e.preventDefault();
          void ejecutar(() => onAprobar(comentario.trim() || null));
        }}
      >
        <label className="flex min-w-0 flex-col gap-1 text-sm font-medium">
          Indicación para el estudiante (opcional)
          <input value={comentario} onChange={(e) => setComentario(e.target.value)} maxLength={500} placeholder="Ej.: Retirala en Bedelía de 18 a 21 h" className={campo} />
        </label>
        <div>
          <button type="submit" className={botonPrimarioChico} disabled={ocupado}>
            Aprobar
          </button>
        </div>
      </form>
      <form
        aria-label="Rechazar trámite"
        className="flex flex-col gap-2"
        onSubmit={(e) => {
          e.preventDefault();
          void ejecutar(() => onRechazar(motivo));
        }}
      >
        <label className="flex min-w-0 flex-col gap-1 text-sm font-medium">
          Motivo del rechazo
          <input value={motivo} onChange={(e) => setMotivo(e.target.value)} required maxLength={500} className={campo} />
        </label>
        <div>
          <button type="submit" className={botonPeligroChico} disabled={ocupado}>
            Rechazar
          </button>
        </div>
      </form>
    </div>
  );
}
