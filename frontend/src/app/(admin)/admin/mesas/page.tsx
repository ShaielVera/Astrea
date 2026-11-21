"use client";

import Link from "next/link";
import { useCallback, useEffect, useRef, useState, type FormEvent } from "react";
import { useAuth } from "@/features/auth/AuthProvider";
import { mensajeDeError } from "@/features/auth/mensajes-error";
import { academicoApi, type Carrera, type Pagina } from "@/features/academico/api";
import { mesasApi, type EstadoMesa, type FiltroMesas, type MesaExamen } from "@/features/mesas/api";
import { FormularioMesa } from "@/features/mesas/FormularioMesa";
import { ListadoMesas } from "@/features/mesas/ListadoMesas";
import { Paginacion } from "@/features/mesas/Paginacion";

const campo = "w-full min-w-0 rounded-md border border-zinc-300 bg-transparent px-3 py-2 font-normal dark:border-zinc-700 dark:bg-zinc-900";
const botonChico =
  "rounded-md border border-zinc-300 px-3 py-1.5 text-sm font-medium hover:bg-zinc-100 dark:border-zinc-700 dark:hover:bg-zinc-800";
const botonPrimarioChico = "rounded-md bg-indigo-600 px-3 py-1.5 text-sm font-medium text-white hover:bg-indigo-700";
const botonPeligroChico =
  "rounded-md border border-red-300 px-3 py-1.5 text-sm font-medium text-red-700 hover:bg-red-50 dark:border-red-900 dark:text-red-300 dark:hover:bg-red-950";
const POR_PAGINA = 10;

export default function AdministracionMesas() {
  const { obtenerToken } = useAuth();
  const [carreras, setCarreras] = useState<Carrera[]>([]);
  const [filtro, setFiltro] = useState<FiltroMesas>({});
  const [pagina, setPagina] = useState<Pagina<MesaExamen> | null>(null);
  const [editando, setEditando] = useState<MesaExamen | null>(null);
  const [cancelando, setCancelando] = useState<string | null>(null);
  const [eliminando, setEliminando] = useState<string | null>(null);
  const [aviso, setAviso] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);
  // Numera las cargas del listado: una respuesta vieja no pisa a la más reciente.
  const ultimaCarga = useRef(0);

  const cargar = useCallback(
    async (criterio: FiltroMesas, page: number) => {
      const numero = ++ultimaCarga.current;
      try {
        const resultado = await mesasApi.buscarAdmin(await obtenerToken(), { ...criterio, page, pageSize: POR_PAGINA });
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
      .then((token) => Promise.all([academicoApi.carreras(token, "admin"), mesasApi.buscarAdmin(token, { page: 1, pageSize: POR_PAGINA })]))
      .then(([lista, primeras]) => {
        if (!vigente) return;
        setCarreras(lista);
        if (numero === ultimaCarga.current) setPagina(primeras);
      })
      .catch((e) => vigente && setError(mensajeDeError(e)));
    return () => {
      vigente = false;
    };
  }, [obtenerToken]);

  /** Ejecuta una acción sobre una mesa y recarga la página actual del listado. */
  async function ejecutar(accion: (token: string) => Promise<unknown>, mensaje: string) {
    setError(null);
    setAviso(null);
    try {
      await accion(await obtenerToken());
      setAviso(mensaje);
      await cargar(filtro, pagina?.page ?? 1);
    } catch (e) {
      setError(mensajeDeError(e));
    }
  }

  function filtrar(evento: FormEvent<HTMLFormElement>) {
    evento.preventDefault();
    void cargar(filtro, 1);
  }

  return (
    <main className="mx-auto flex w-full max-w-5xl flex-1 flex-col gap-6 px-4 py-10 sm:px-6">
      <div className="flex flex-col gap-1">
        <Link href="/admin" className="text-sm text-indigo-600 hover:underline dark:text-indigo-400">
          ← Administración
        </Link>
        <h1 className="text-3xl font-semibold tracking-tight">Mesas de examen</h1>
        <p className="text-zinc-600 dark:text-zinc-400">
          Cargá cada llamado como borrador y publicalo cuando esté confirmado. Fechas y horas en hora de Argentina.
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

      {editando ? (
        <FormularioMesa
          key={editando.id}
          carreras={carreras}
          mesa={editando}
          onCancelar={() => setEditando(null)}
          onGuardar={async (datos) => {
            await mesasApi.actualizar(await obtenerToken(), editando.id, datos);
            setEditando(null);
            setAviso("Mesa actualizada.");
            await cargar(filtro, pagina?.page ?? 1);
          }}
        />
      ) : (
        <FormularioMesa
          carreras={carreras}
          onGuardar={async (datos) => {
            const creada = await mesasApi.crear(await obtenerToken(), datos);
            setAviso(`Borrador de ${creada.materiaNombre} guardado. Publicalo para que lo vean los estudiantes.`);
            await cargar(filtro, 1);
          }}
        />
      )}

      <form onSubmit={filtrar} aria-label="Filtrar mesas" className="flex flex-wrap items-end gap-3">
        <label className="flex min-w-0 flex-col gap-1 text-sm font-medium">
          Estado
          <select
            value={filtro.estado ?? ""}
            onChange={(e) => {
              // El estado se aplica en el momento (la búsqueda por texto, con "Filtrar").
              const nuevo = { ...filtro, estado: (e.target.value || undefined) as EstadoMesa | undefined };
              setFiltro(nuevo);
              void cargar(nuevo, 1);
            }}
            className={campo}
          >
            <option value="">Todos</option>
            <option value="Borrador">Borradores</option>
            <option value="Publicada">Publicadas</option>
            <option value="Cancelada">Canceladas</option>
          </select>
        </label>
        <label className="flex min-w-0 flex-col gap-1 text-sm font-medium">
          Desde
          <input type="date" value={filtro.desde ?? ""} onChange={(e) => setFiltro((f) => ({ ...f, desde: e.target.value || undefined }))} className={campo} />
        </label>
        <label className="flex min-w-48 flex-1 flex-col gap-1 text-sm font-medium">
          Materia o docente
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

      <section aria-label="Mesas cargadas" className="flex flex-col gap-4">
        {pagina?.totalItems === 0 && <p className="text-sm text-zinc-500">No hay mesas con esos filtros.</p>}
        {pagina && (
          <ListadoMesas
            mesas={pagina.items}
            mostrarEstado
            acciones={(mesa) =>
              eliminando === mesa.id ? (
                <div role="alertdialog" aria-label={`Eliminar el borrador de ${mesa.materiaNombre}`} className="flex flex-wrap items-center gap-2 text-sm">
                  ¿Eliminar este borrador? No se puede deshacer.
                  <button
                    type="button"
                    className={botonPeligroChico}
                    onClick={() =>
                      ejecutar(async (token) => {
                        await mesasApi.eliminar(token, mesa.id);
                        setEliminando(null);
                        // Si se estaba editando esa mesa, el formulario vuelve a "Nueva mesa".
                        if (editando?.id === mesa.id) setEditando(null);
                      }, "Borrador eliminado.")
                    }
                  >
                    Sí, eliminar
                  </button>
                  <button type="button" className={botonChico} onClick={() => setEliminando(null)}>
                    No
                  </button>
                </div>
              ) : cancelando === mesa.id ? (
                <FormularioCancelacion
                  mesa={mesa}
                  onDescartar={() => setCancelando(null)}
                  onConfirmar={(motivo) =>
                    ejecutar(async (token) => {
                      await mesasApi.cancelar(token, mesa.id, motivo);
                      setCancelando(null);
                    }, `Mesa de ${mesa.materiaNombre} cancelada.`)
                  }
                />
              ) : (
                <>
                  {mesa.estado === "Borrador" && (
                    <button
                      type="button"
                      className={botonPrimarioChico}
                      aria-label={`Publicar ${mesa.materiaNombre}`}
                      onClick={() => ejecutar((token) => mesasApi.publicar(token, mesa.id), `Mesa de ${mesa.materiaNombre} publicada.`)}
                    >
                      Publicar
                    </button>
                  )}
                  {mesa.estado !== "Cancelada" && (
                    <button
                      type="button"
                      className={botonChico}
                      aria-label={`Editar ${mesa.materiaNombre}`}
                      onClick={() => {
                        setEditando(mesa);
                        window.scrollTo({ top: 0, behavior: "smooth" });
                      }}
                    >
                      Editar
                    </button>
                  )}
                  {mesa.estado === "Borrador" && (
                    <button
                      type="button"
                      className={botonPeligroChico}
                      aria-label={`Eliminar ${mesa.materiaNombre}`}
                      onClick={() => setEliminando(mesa.id)}
                    >
                      Eliminar
                    </button>
                  )}
                  {mesa.estado === "Publicada" && (
                    <button
                      type="button"
                      className={botonPeligroChico}
                      aria-label={`Cancelar ${mesa.materiaNombre}`}
                      onClick={() => setCancelando(mesa.id)}
                    >
                      Cancelar mesa
                    </button>
                  )}
                </>
              )
            }
          />
        )}
        {pagina && <Paginacion pagina={pagina} onCambiar={(page) => void cargar(filtro, page)} />}
      </section>
    </main>
  );
}

/** Pide el motivo antes de cancelar: los estudiantes lo ven junto a la mesa. */
function FormularioCancelacion({
  mesa,
  onConfirmar,
  onDescartar,
}: {
  mesa: MesaExamen;
  onConfirmar: (motivo: string) => Promise<void>;
  onDescartar: () => void;
}) {
  const [motivo, setMotivo] = useState("");

  return (
    <form
      aria-label={`Cancelar mesa de ${mesa.materiaNombre}`}
      className="flex w-full flex-col gap-2 sm:w-64"
      onSubmit={(e) => {
        e.preventDefault();
        void onConfirmar(motivo);
      }}
    >
      <label className="flex min-w-0 flex-col gap-1 text-sm font-medium">
        Motivo de la cancelación
        <input value={motivo} onChange={(e) => setMotivo(e.target.value)} required maxLength={300} className={campo} />
      </label>
      <div className="flex gap-2">
        <button type="submit" className={botonPeligroChico}>
          Confirmar cancelación
        </button>
        <button type="button" className={botonChico} onClick={onDescartar}>
          Volver
        </button>
      </div>
    </form>
  );
}
