"use client";

import { useEffect, useRef, useState } from "react";
import { useAuth } from "@/features/auth/AuthProvider";
import { mensajeDeError } from "@/features/auth/mensajes-error";
import { academicoApi, type Carrera, type Pagina } from "@/features/academico/api";
import { Paginacion } from "@/features/mesas/Paginacion";
import { fechaHora, tramitesApi, type TramiteDetalle, type TramiteResumen, type TramiteTipo } from "@/features/tramites/api";
import { DetalleTramite, EstadoBadge } from "@/features/tramites/DetalleTramite";
import { perfilApi } from "@/features/perfil/api";
import Link from "next/link";
import { FormularioTramite } from "@/features/tramites/FormularioTramite";

const campo = "w-full min-w-0 rounded-md border border-zinc-300 bg-transparent px-3 py-2 font-normal dark:border-zinc-700 dark:bg-zinc-900";

export default function TramitesPage() {
  const { obtenerToken } = useAuth();
  const [tipos, setTipos] = useState<TramiteTipo[] | null>(null);
  const [carreras, setCarreras] = useState<Carrera[]>([]);
  const [tipoId, setTipoId] = useState("");
  const [mios, setMios] = useState<Pagina<TramiteResumen> | null>(null);
  const [seleccionado, setSeleccionado] = useState<TramiteDetalle | null>(null);
  const [aviso, setAviso] = useState<string | null>(null);
  const [sinDatos, setSinDatos] = useState(false);
  const [error, setError] = useState<string | null>(null);
  // Una clave por solicitud: si el envío se repite (doble clic, reintento), el backend crea un solo trámite.
  const claveIdempotencia = useRef<string | null>(null);

  useEffect(() => {
    let vigente = true;
    obtenerToken()
      .then((token) => Promise.all([tramitesApi.tipos(token), academicoApi.carreras(token), tramitesApi.mios(token)]))
      .then(([listaTipos, listaCarreras, pagina]) => {
        if (!vigente) return;
        setTipos(listaTipos);
        setCarreras(listaCarreras);
        setMios(pagina);
      })
      .catch((e) => vigente && setError(mensajeDeError(e)));
    obtenerToken()
      .then((token) => perfilApi.obtener(token))
      .then((miPerfil) => vigente && setSinDatos(!miPerfil.nombre || !miPerfil.apellido || !miPerfil.dni))
      .catch((e) => vigente && setError(mensajeDeError(e)));
    return () => {
      vigente = false;
    };
  }, [obtenerToken]);

  async function cargarMios(page: number) {
    try {
      setMios(await tramitesApi.mios(await obtenerToken(), page));
    } catch (e) {
      setError(mensajeDeError(e));
    }
  }

  async function verDetalle(id: string) {
    setError(null);
    try {
      setSeleccionado(await tramitesApi.detalle(await obtenerToken(), id));
    } catch (e) {
      setError(mensajeDeError(e));
    }
  }

  async function descargar(id: string) {
    setError(null);
    try {
      await tramitesApi.descargar(await obtenerToken(), id);
    } catch (e) {
      setError(mensajeDeError(e));
    }
  }

  async function solicitar(datos: Record<string, string>) {
    setError(null);
    setAviso(null);
    claveIdempotencia.current ??= crypto.randomUUID();
    try {
      const token = await obtenerToken();
      const creado = await tramitesApi.solicitar(token, { tipoId, datos, claveIdempotencia: claveIdempotencia.current });
      claveIdempotencia.current = null;
      setTipoId("");
      setSeleccionado(creado);
      setAviso(`Listo: pediste ${creado.tipoNombre}. Tu número de trámite es ${creado.numero}.`);
      setMios(await tramitesApi.mios(token));
    } catch (e) {
      setError(mensajeDeError(e));
    }
  }

  async function cancelar(id: string) {
    setError(null);
    setAviso(null);
    try {
      const token = await obtenerToken();
      const cancelado = await tramitesApi.cancelar(token, id);
      setSeleccionado(cancelado);
      setAviso(`Cancelaste el trámite ${cancelado.numero}.`);
      setMios(await tramitesApi.mios(token, mios?.page ?? 1));
    } catch (e) {
      setError(mensajeDeError(e));
    }
  }

  const tipo = tipos?.find((t) => t.id === tipoId);

  return (
    <main className="mx-auto flex w-full max-w-5xl flex-1 flex-col gap-6 px-4 py-10 sm:px-6">
      <div className="flex flex-col gap-1">
        <h1 className="text-3xl font-semibold tracking-tight">Mis trámites</h1>
        <p className="text-zinc-600 dark:text-zinc-400">Pedí constancias y certificados, y seguí en qué estado está cada pedido.</p>
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

      <section aria-labelledby="pedir-titulo" className="flex flex-col gap-3 rounded-xl border bg-superficie border-zinc-200 p-4 dark:border-zinc-800">
        <h2 id="pedir-titulo" className="text-lg font-semibold">
          Pedir un trámite
        </h2>
        <label className="flex min-w-0 flex-col gap-1 text-sm font-medium sm:max-w-md">
          Tipo de trámite
          <select
            value={tipoId}
            onChange={(e) => {
              setTipoId(e.target.value);
              claveIdempotencia.current = null;
            }}
            disabled={!tipos}
            className={campo}
          >
            <option value="">{tipos ? "Elegí un trámite" : "Cargando…"}</option>
            {tipos?.map((t) => (
              <option key={t.id} value={t.id}>
                {t.nombre}
              </option>
            ))}
          </select>
        </label>
        {sinDatos && (
          <p className="rounded-md bg-amber-50 px-3 py-2 text-sm text-amber-900 dark:bg-amber-950 dark:text-amber-200">
            Antes de pedir una constancia, completá tu nombre, apellido y DNI en{" "}
            <Link href="/mi-perfil" className="font-semibold underline">
              Mi perfil
            </Link>
            : son los que van impresos.
          </p>
        )}
        {tipo && (
          <FormularioTramite
            key={tipo.id}
            tipo={tipo}
            carreras={carreras}
            onEnviar={solicitar}
            onCancelar={() => {
              setTipoId("");
              claveIdempotencia.current = null;
            }}
          />
        )}
      </section>

      <div className="grid grid-cols-1 gap-6 lg:grid-cols-[minmax(0,2fr)_minmax(0,3fr)]">
        <section aria-labelledby="mios-titulo" className="flex min-w-0 flex-col gap-3">
          <h2 id="mios-titulo" className="text-lg font-semibold">
            Tus pedidos
          </h2>
          {mios?.totalItems === 0 && <p className="text-sm text-zinc-500">Todavía no pediste ningún trámite.</p>}
          <ul className="flex flex-col gap-2">
            {mios?.items.map((tramite) => (
              <li key={tramite.id}>
                <button
                  type="button"
                  onClick={() => void verDetalle(tramite.id)}
                  aria-current={seleccionado?.id === tramite.id}
                  className="flex w-full flex-col gap-1 rounded-lg border border-zinc-200 bg-superficie px-3 py-2 text-left shadow-sm hover:border-indigo-400 aria-[current=true]:border-indigo-500 dark:border-zinc-800"
                >
                  <span className="flex flex-wrap items-center justify-between gap-2">
                    <span className="font-medium">{tramite.tipoNombre}</span>
                    <EstadoBadge estado={tramite.estado} />
                  </span>
                  <span className="text-xs text-zinc-500">
                    {tramite.numero} · {fechaHora(tramite.fechaSolicitud)}
                  </span>
                </button>
              </li>
            ))}
          </ul>
          {mios && <Paginacion pagina={mios} onCambiar={(page) => void cargarMios(page)} />}
        </section>

        <div className="min-w-0">
          {seleccionado ? (
            <DetalleTramite
              tramite={seleccionado}
              onDescargar={() => void descargar(seleccionado.id)}
              acciones={
                (seleccionado.estado === "Pendiente" || seleccionado.estado === "EnProceso") && (
                  <CancelarTramite key={seleccionado.id} onConfirmar={() => cancelar(seleccionado.id)} />
                )
              }
            />
          ) : (
            mios && mios.totalItems > 0 && <p className="text-sm text-zinc-500">Elegí un pedido para ver su detalle e historial.</p>
          )}
        </div>
      </div>
    </main>
  );
}

/** Cancelar pide una confirmación en el lugar (sin ventanas emergentes). */
function CancelarTramite({ onConfirmar }: { onConfirmar: () => Promise<void> }) {
  const [confirmando, setConfirmando] = useState(false);
  const [ocupado, setOcupado] = useState(false);
  const boton = "rounded-md border px-3 py-1.5 text-sm font-medium disabled:opacity-60";

  if (!confirmando) {
    return (
      <button type="button" onClick={() => setConfirmando(true)} className={`${boton} self-start border-red-300 text-red-700 hover:bg-red-50 dark:border-red-900 dark:text-red-300 dark:hover:bg-red-950`}>
        Cancelar trámite
      </button>
    );
  }

  return (
    <div className="flex flex-wrap items-center gap-2 text-sm">
      <span>¿Cancelar este trámite? Si lo necesitás de nuevo, vas a tener que pedirlo otra vez.</span>
      <button
        type="button"
        disabled={ocupado}
        onClick={async () => {
          setOcupado(true);
          await onConfirmar();
          setOcupado(false);
        }}
        className={`${boton} border-red-300 bg-red-600 text-white hover:bg-red-700 dark:border-red-900`}
      >
        {ocupado ? "Cancelando…" : "Sí, cancelar"}
      </button>
      <button type="button" disabled={ocupado} onClick={() => setConfirmando(false)} className={`${boton} border-zinc-300 hover:bg-zinc-100 dark:border-zinc-700 dark:hover:bg-zinc-800`}>
        No
      </button>
    </div>
  );
}
