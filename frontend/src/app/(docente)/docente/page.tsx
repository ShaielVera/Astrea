"use client";

import { useCallback, useEffect, useState, type FormEvent } from "react";
import { useAuth } from "@/features/auth/AuthProvider";
import { mensajeDeError } from "@/features/auth/mensajes-error";
import { academicoApi, agruparPorAnio, NOMBRE_ANIO, type Carrera, type MateriaDePlan } from "@/features/academico/api";
import { OpcionesCarrera } from "@/features/perfil/OpcionesCarrera";
import {
  asistenciaApi,
  fechaCorta,
  VALORES,
  type MateriaCursada,
  type MateriaAConfirmar,
  type PanelDocente,
  type Relevamiento,
  type ValorAsistencia,
} from "@/features/asistencia/api";

export default function AsistenciaDocente() {
  const { obtenerToken } = useAuth();
  const [relevamientos, setRelevamientos] = useState<Relevamiento[] | null>(null);
  const [elegido, setElegido] = useState("");
  const [panel, setPanel] = useState<PanelDocente | null>(null);
  const [error, setError] = useState<string | null>(null);
  /** Sube cada vez que el docente cambia sus materias: el panel se vuelve a pedir para mostrarlas. */
  const [version, setVersion] = useState(0);

  useEffect(() => {
    let vigente = true;
    obtenerToken()
      .then((token) => asistenciaApi.relevamientosDocente(token))
      .then((lista) => {
        if (!vigente) return;
        setRelevamientos(lista);
        setElegido(lista.find((relevamiento) => relevamiento.abierto)?.id ?? lista[0]?.id ?? "");
      })
      .catch((e) => vigente && setError(mensajeDeError(e)));
    return () => {
      vigente = false;
    };
  }, [obtenerToken]);

  useEffect(() => {
    if (!elegido) return;
    let vigente = true;
    obtenerToken()
      .then((token) => asistenciaApi.panel(token, elegido))
      .then((resultado) => vigente && setPanel(resultado))
      .catch((e) => vigente && setError(mensajeDeError(e)));
    return () => {
      vigente = false;
    };
  }, [elegido, obtenerToken, version]);

  return (
    <main className="mx-auto flex w-full max-w-5xl flex-1 flex-col gap-8 px-4 py-10 sm:px-6">
      <div className="flex flex-col gap-1">
        <h1 className="text-3xl font-semibold tracking-tight">Confirmar asistencia</h1>
        <p className="text-zinc-600 dark:text-zinc-400">
          La dirección pide que confirmes quién asiste en tus materias. Marcá a cada estudiante según lo que ves en tus clases.
        </p>
      </div>

      {error && (
        <p role="alert" className="text-red-700 dark:text-red-300">
          {error}
        </p>
      )}

      <MisMaterias onCambio={() => setVersion((actual) => actual + 1)} />

      {relevamientos?.length === 0 && <p className="text-zinc-500">No hay pedidos de confirmación de asistencia este año.</p>}

      {relevamientos && relevamientos.length > 0 && (
        <label className="flex max-w-md flex-col gap-1 text-sm font-medium">
          Pedido de la dirección
          <select
            value={elegido}
            onChange={(e) => {
              // Solo si cambia: con el mismo pedido, borrar el panel lo dejaría vacío (no se vuelve a cargar).
              if (e.target.value === elegido) return;
              setPanel(null);
              setElegido(e.target.value);
            }}
            className="rounded-md border border-zinc-300 bg-transparent px-3 py-2 dark:border-zinc-700 dark:bg-zinc-900"
          >
            {relevamientos.map((relevamiento) => (
              <option key={relevamiento.id} value={relevamiento.id}>
                {relevamiento.titulo} — {relevamiento.abierto ? `abierto hasta el ${fechaCorta(relevamiento.fechaCierre)}` : "cerrado"}
              </option>
            ))}
          </select>
        </label>
      )}

      {panel && panel.materias.length === 0 && (
        <p className="text-zinc-500">Todavía no marcaste las materias que das: elegilas arriba, en “Las materias que doy”.</p>
      )}

      {panel?.materias.map((materia) => (
        <TablaMateria key={`${panel.relevamiento.id}-${materia.planMateriaId}`} relevamiento={panel.relevamiento} materia={materia} />
      ))}
    </main>
  );
}

/**
 * El docente marca con un tilde las materias que da (y se lo saca a las que eligió por error). Cada tilde se guarda al
 * tocarlo; Secretaría ve lo elegido en "Usuarios y roles" y lo puede corregir.
 */
function MisMaterias({ onCambio }: { onCambio: () => void }) {
  const { obtenerToken } = useAuth();
  const [mias, setMias] = useState<MateriaCursada[] | null>(null);
  const [carreras, setCarreras] = useState<Carrera[] | null>(null);
  const [carreraId, setCarreraId] = useState("");
  const [materias, setMaterias] = useState<MateriaDePlan[] | null>(null);
  /** El tilde cambia al tocarlo, sin esperar al servidor; si no se pudo guardar, vuelve como estaba. */
  const [pendientes, setPendientes] = useState<Record<string, boolean>>({});
  const [error, setError] = useState<string | null>(null);

  const elegirCarrera = useCallback(
    async (id: string) => {
      setCarreraId(id);
      setMaterias(null);
      if (!id) return;
      try {
        const token = await obtenerToken();
        const planes = await academicoApi.planes(token, id);
        const vigente = planes.find((plan) => plan.activo) ?? planes[0];
        setMaterias(vigente ? await academicoApi.materias(token, vigente.id) : []);
      } catch (e) {
        setError(mensajeDeError(e));
      }
    },
    [obtenerToken],
  );

  useEffect(() => {
    let vigente = true;
    obtenerToken()
      .then((token) => Promise.all([asistenciaApi.misMaterias(token), academicoApi.carreras(token)]))
      .then(([propias, todas]) => {
        if (!vigente) return;
        setMias(propias);
        setCarreras(todas);
        // Abre directamente la carrera en la que ya da clases.
        if (propias[0]) void elegirCarrera(propias[0].carreraId);
      })
      .catch((e) => vigente && setError(mensajeDeError(e)));
    return () => {
      vigente = false;
    };
  }, [obtenerToken, elegirCarrera]);

  async function cambiar(materia: MateriaDePlan, marcar: boolean) {
    const id = materia.planMateriaId;
    setError(null);
    setPendientes((actuales) => ({ ...actuales, [id]: marcar }));
    try {
      const token = await obtenerToken();
      if (marcar) await asistenciaApi.marcarMateria(token, id);
      else await asistenciaApi.desmarcarMateria(token, id);
      setMias(await asistenciaApi.misMaterias(token));
      onCambio();
    } catch (e) {
      setError(mensajeDeError(e));
    } finally {
      setPendientes((actuales) => Object.fromEntries(Object.entries(actuales).filter(([clave]) => clave !== id)));
    }
  }

  const marcadas = new Set(mias?.map((materia) => materia.planMateriaId));
  const marcada = (id: string) => pendientes[id] ?? marcadas.has(id);

  return (
    <section aria-labelledby="mis-materias-titulo" className="flex flex-col gap-4 rounded-xl border border-zinc-200 bg-superficie p-5 shadow-sm dark:border-zinc-800">
      <div className="flex flex-col gap-1">
        <h2 id="mis-materias-titulo" className="text-lg font-semibold">
          Las materias que doy
        </h2>
        <p className="text-sm text-zinc-600 dark:text-zinc-400">
          Elegí la carrera y marcá con un tilde las materias que das. Si marcaste una por error, tocala de nuevo para sacarle el tilde. Se guarda solo.
        </p>
      </div>

      {mias && mias.length > 0 && (
        <ul aria-label="Materias que doy" className="flex flex-wrap gap-2">
          {mias.map((materia) => (
            <li key={materia.planMateriaId} className="rounded-full bg-emerald-100 px-3 py-1 text-sm text-emerald-900 dark:bg-emerald-950 dark:text-emerald-200">
              ✓ {materia.materia} <span className="text-emerald-800/80 dark:text-emerald-300/80">· {materia.carrera}</span>
            </li>
          ))}
        </ul>
      )}

      <label className="flex max-w-md flex-col gap-1 text-sm font-medium">
        Carrera
        <select
          value={carreraId}
          onChange={(e) => void elegirCarrera(e.target.value)}
          disabled={!carreras}
          className="rounded-md border border-zinc-300 bg-transparent px-3 py-2 dark:border-zinc-700 dark:bg-zinc-900"
        >
          <option value="">Elegí una carrera</option>
          <OpcionesCarrera carreras={carreras} mias={mias?.map((materia) => materia.carreraId) ?? null} />
        </select>
      </label>

      {carreraId && !materias && !error && (
        <p role="status" className="text-sm text-zinc-500">
          Cargando materias…
        </p>
      )}
      {materias?.length === 0 && <p className="text-sm text-zinc-500">Esta carrera todavía no tiene materias cargadas.</p>}

      {materias && materias.length > 0 && (
        <div className="flex flex-col gap-4">
          {agruparPorAnio(materias).map(([anio, delAnio]) => (
            <fieldset key={anio} className="flex flex-col gap-1">
              <legend className="mb-1 text-sm font-semibold text-zinc-700 dark:text-zinc-300">{NOMBRE_ANIO[anio] ?? `Año ${anio}`}</legend>
              {delAnio.map((materia) => (
                <label
                  key={materia.planMateriaId}
                  className="flex min-h-11 items-center gap-3 rounded-md px-2 py-1.5 hover:bg-zinc-100 has-[:checked]:bg-emerald-50 dark:hover:bg-zinc-800 dark:has-[:checked]:bg-emerald-950/50"
                >
                  <input
                    type="checkbox"
                    className="size-5 accent-emerald-600"
                    checked={marcada(materia.planMateriaId)}
                    disabled={!mias || materia.planMateriaId in pendientes}
                    onChange={(e) => void cambiar(materia, e.target.checked)}
                  />
                  <span>{materia.nombre}</span>
                </label>
              ))}
            </fieldset>
          ))}
        </div>
      )}

      {error && (
        <p role="alert" className="text-sm text-red-700 dark:text-red-300">
          {error}
        </p>
      )}
    </section>
  );
}

type Fila = { valor: ValorAsistencia | ""; observacion: string };

function TablaMateria({ relevamiento, materia }: { relevamiento: Relevamiento; materia: MateriaAConfirmar }) {
  const { obtenerToken } = useAuth();
  const [filas, setFilas] = useState<Record<string, Fila>>(() =>
    Object.fromEntries(materia.estudiantes.map((estudiante) => [estudiante.id, { valor: estudiante.valor ?? "", observacion: estudiante.observacion ?? "" }])),
  );
  const [estado, setEstado] = useState<{ tipo: "ok" | "error"; mensaje: string } | null>(null);
  const [guardando, setGuardando] = useState(false);
  const soloLectura = !relevamiento.abierto;
  const titulo = `${materia.materia} (${materia.carrera}, ${NOMBRE_ANIO[materia.anio] ?? `año ${materia.anio}`})`;

  function cambiar(id: string, cambios: Partial<Fila>) {
    setEstado(null);
    setFilas((actuales) => ({ ...actuales, [id]: { ...actuales[id], ...cambios } }));
  }

  async function guardar(evento: FormEvent<HTMLFormElement>) {
    evento.preventDefault();
    const confirmaciones = Object.entries(filas)
      .filter(([, fila]) => fila.valor !== "")
      .map(([estudianteId, fila]) => ({ estudianteId, valor: fila.valor as ValorAsistencia, observacion: fila.observacion.trim() || null }));
    if (confirmaciones.length === 0) {
      setEstado({ tipo: "error", mensaje: "Marcá al menos a un estudiante." });
      return;
    }

    setGuardando(true);
    try {
      const guardadas = await asistenciaApi.confirmar(await obtenerToken(), relevamiento.id, materia.planMateriaId, confirmaciones);
      const faltan = materia.estudiantes.length - confirmaciones.length;
      setEstado({ tipo: "ok", mensaje: `Guardado: ${guardadas} ${guardadas === 1 ? "estudiante" : "estudiantes"}.${faltan > 0 ? ` Faltan ${faltan}.` : ""}` });
    } catch (e) {
      setEstado({ tipo: "error", mensaje: mensajeDeError(e) });
    } finally {
      setGuardando(false);
    }
  }

  return (
    <section aria-label={titulo} className="flex flex-col gap-3 rounded-xl border bg-superficie border-zinc-200 p-4 dark:border-zinc-800">
      <h2 className="text-lg font-semibold">
        {materia.materia}
        <span className="block text-sm font-normal text-zinc-500">
          {materia.carrera} · {NOMBRE_ANIO[materia.anio] ?? `Año ${materia.anio}`} · {materia.estudiantes.length}{" "}
          {materia.estudiantes.length === 1 ? "estudiante anotado" : "estudiantes anotados"}
        </span>
      </h2>

      {materia.estudiantes.length === 0 ? (
        <p className="text-sm text-zinc-500">Todavía no hay estudiantes anotados en esta materia.</p>
      ) : (
        <form onSubmit={guardar} aria-label={`Confirmar asistencia de ${materia.materia}`} className="flex flex-col gap-3">
          {!soloLectura && (
            <p className="text-sm text-zinc-600 dark:text-zinc-400">
              Si un estudiante faltó seguido, marcalo “Asiste de forma irregular”; si dejó de venir, “No asiste”.
            </p>
          )}
          <ul className="flex flex-col divide-y divide-zinc-100 dark:divide-zinc-900">
            {materia.estudiantes.map((estudiante) => (
              <li key={estudiante.id} className="flex flex-col gap-2 py-3 lg:flex-row lg:items-center lg:justify-between">
                <span className="min-w-0">
                  <span className="font-medium">{estudiante.nombre}</span>
                  {estudiante.nombre !== estudiante.email && <span className="block truncate text-sm text-zinc-500">{estudiante.email}</span>}
                </span>
                <fieldset className="flex flex-wrap gap-x-4 gap-y-1" disabled={soloLectura}>
                  <legend className="sr-only">Asistencia de {estudiante.nombre}</legend>
                  {VALORES.map((opcion) => (
                    <label key={opcion.valor} className="flex items-center gap-1.5 text-sm">
                      <input
                        type="radio"
                        name={`${materia.planMateriaId}-${estudiante.id}`}
                        checked={filas[estudiante.id]?.valor === opcion.valor}
                        onChange={() => cambiar(estudiante.id, { valor: opcion.valor })}
                      />
                      {opcion.etiqueta}
                    </label>
                  ))}
                </fieldset>
                <input
                  type="text"
                  aria-label={`Observación sobre ${estudiante.nombre}`}
                  placeholder="Observación (opcional)"
                  maxLength={200}
                  disabled={soloLectura}
                  value={filas[estudiante.id]?.observacion ?? ""}
                  onChange={(e) => cambiar(estudiante.id, { observacion: e.target.value })}
                  className="min-w-0 rounded-md border border-zinc-300 bg-transparent px-3 py-1.5 text-sm lg:w-56 dark:border-zinc-700 dark:bg-zinc-900"
                />
              </li>
            ))}
          </ul>
          {soloLectura ? (
            <p className="text-sm text-zinc-500">Este pedido está cerrado: ya no se pueden cambiar las confirmaciones.</p>
          ) : (
            <button type="submit" disabled={guardando} className="self-start rounded-md bg-indigo-600 px-4 py-2 font-medium text-white hover:bg-indigo-700 disabled:opacity-60">
              {guardando ? "Guardando…" : "Guardar"}
            </button>
          )}
          {estado && (
            <p role={estado.tipo === "ok" ? "status" : "alert"} className={estado.tipo === "ok" ? "text-emerald-700 dark:text-emerald-300" : "text-red-700 dark:text-red-300"}>
              {estado.mensaje}
            </p>
          )}
        </form>
      )}
    </section>
  );
}
