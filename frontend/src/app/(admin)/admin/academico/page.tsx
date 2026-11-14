"use client";

import { useCallback, useEffect, useState, type FormEvent, type ReactNode } from "react";
import { useAuth } from "@/features/auth/AuthProvider";
import { mensajeDeError } from "@/features/auth/mensajes-error";
import {
  academicoApi,
  ETIQUETA_CUATRIMESTRE,
  type Carrera,
  type Cuatrimestre,
  type MateriaDePlan,
  type PlanEstudio,
} from "@/features/academico/api";
import { EditorCorrelatividades } from "@/features/academico/EditorCorrelatividades";
import { ListaMateriasPorAnio } from "@/features/academico/ListaMateriasPorAnio";

const campo = "rounded-md border border-zinc-300 bg-transparent px-3 py-2 dark:border-zinc-700 dark:bg-zinc-900";
const botonPrimario = "rounded-md bg-indigo-600 px-4 py-2 font-medium text-white hover:bg-indigo-700 disabled:opacity-60";
const botonSecundario =
  "rounded-md border border-zinc-300 px-3 py-1.5 text-sm font-medium hover:bg-zinc-100 dark:border-zinc-700 dark:hover:bg-zinc-800";

export default function AdministracionAcademica() {
  const { obtenerToken } = useAuth();
  const [carreras, setCarreras] = useState<Carrera[]>([]);
  const [carrera, setCarrera] = useState<Carrera | null>(null);
  const [planes, setPlanes] = useState<PlanEstudio[]>([]);
  const [plan, setPlan] = useState<PlanEstudio | null>(null);
  const [materias, setMaterias] = useState<MateriaDePlan[]>([]);
  const [editando, setEditando] = useState<MateriaDePlan | null>(null);
  const [correlativasDe, setCorrelativasDe] = useState<MateriaDePlan | null>(null);
  const [error, setError] = useState<string | null>(null);

  /** Ejecuta una acción contra la API mostrando el error, si lo hay. */
  const ejecutar = useCallback(
    async (accion: (token: string) => Promise<void>) => {
      setError(null);
      try {
        await accion(await obtenerToken());
      } catch (e) {
        setError(mensajeDeError(e));
      }
    },
    [obtenerToken],
  );

  useEffect(() => {
    let vigente = true;
    obtenerToken()
      .then((token) => academicoApi.carreras(token, "admin"))
      .then((lista) => vigente && setCarreras(lista))
      .catch((e) => vigente && setError(mensajeDeError(e)));
    return () => {
      vigente = false;
    };
  }, [obtenerToken]);

  async function elegirCarrera(elegida: Carrera) {
    setCarrera(elegida);
    setPlan(null);
    setMaterias([]);
    await ejecutar(async (token) => {
      const lista = await academicoApi.planes(token, elegida.id, "admin");
      setPlanes(lista);
      if (lista[0]) await elegirPlan(lista[0], token);
    });
  }

  async function elegirPlan(elegido: PlanEstudio, token?: string) {
    setPlan(elegido);
    setEditando(null);
    setCorrelativasDe(null);
    const cargar = async (t: string) => setMaterias(await academicoApi.materias(t, elegido.id, "admin"));
    if (token) await cargar(token);
    else await ejecutar(cargar);
  }

  return (
    <main className="mx-auto flex w-full max-w-6xl flex-1 flex-col gap-6 px-4 py-10 sm:px-6">
      <div className="flex flex-col gap-1">
        <h1 className="text-3xl font-semibold tracking-tight">Plan de estudios</h1>
        <p className="text-zinc-600 dark:text-zinc-400">Carreras, planes y materias del instituto.</p>
      </div>

      {error && (
        <p role="alert" className="rounded-md bg-red-50 px-3 py-2 text-sm text-red-700 dark:bg-red-950 dark:text-red-300">
          {error}
        </p>
      )}

      <div className="grid gap-6 lg:grid-cols-[18rem_1fr]">
        <aside className="flex flex-col gap-4">
          <section aria-labelledby="carreras-titulo" className="flex flex-col gap-2">
            <h2 id="carreras-titulo" className="font-semibold">
              Carreras
            </h2>
            <ul className="flex flex-col gap-1">
              {carreras.map((c) => (
                <li key={c.id}>
                  <button
                    type="button"
                    onClick={() => void elegirCarrera(c)}
                    aria-current={carrera?.id === c.id}
                    className={`w-full rounded-md px-3 py-2 text-left text-sm hover:bg-zinc-100 dark:hover:bg-zinc-800 ${
                      carrera?.id === c.id ? "bg-indigo-50 font-medium dark:bg-indigo-950" : ""
                    } ${c.activa ? "" : "text-zinc-400"}`}
                  >
                    <span className="block">{c.codigo}</span>
                    <span className="block text-xs text-zinc-500">{c.nombre}</span>
                  </button>
                </li>
              ))}
            </ul>
          </section>

          <FormularioCarrera
            onCrear={(datos) =>
              ejecutar(async (token) => {
                const nueva = await academicoApi.crearCarrera(token, datos);
                setCarreras(await academicoApi.carreras(token, "admin"));
                await elegirCarrera(nueva);
              })
            }
          />
        </aside>

        {carrera ? (
          <section aria-labelledby="carrera-titulo" className="flex min-w-0 flex-col gap-5">
            <div className="flex flex-wrap items-start justify-between gap-3">
              <div>
                <h2 id="carrera-titulo" className="text-xl font-semibold">
                  {carrera.nombre}
                </h2>
                <p className="text-sm text-zinc-500">
                  {carrera.codigo} · {carrera.duracionAnios} años · {carrera.activa ? "Activa" : "Inactiva"}
                </p>
              </div>
              <button
                type="button"
                className={botonSecundario}
                onClick={() =>
                  ejecutar(async (token) => {
                    const actualizada = await academicoApi.actualizarCarrera(token, { ...carrera, activa: !carrera.activa });
                    setCarrera(actualizada);
                    setCarreras((lista) => lista.map((c) => (c.id === actualizada.id ? actualizada : c)));
                  })
                }
              >
                {carrera.activa ? "Desactivar carrera" : "Activar carrera"}
              </button>
            </div>

            <div className="flex flex-wrap items-end gap-3">
              <label className="flex flex-col gap-1 text-sm font-medium">
                Plan
                <select
                  className={campo}
                  value={plan?.id ?? ""}
                  onChange={(e) => {
                    const elegido = planes.find((p) => p.id === e.target.value);
                    if (elegido) void elegirPlan(elegido);
                  }}
                >
                  {planes.length === 0 && <option value="">Sin planes</option>}
                  {planes.map((p) => (
                    <option key={p.id} value={p.id}>
                      {p.nombre} {p.activo ? "" : "(inactivo)"}
                    </option>
                  ))}
                </select>
              </label>
              <FormularioPlan
                onCrear={(datos) =>
                  ejecutar(async (token) => {
                    const nuevo = await academicoApi.crearPlan(token, carrera.id, datos);
                    setPlanes(await academicoApi.planes(token, carrera.id, "admin"));
                    await elegirPlan(nuevo, token);
                  })
                }
              />
            </div>

            {plan && (
              <>
                <FormularioMateria
                  duracionAnios={carrera.duracionAnios}
                  onAgregar={(datos) =>
                    ejecutar(async (token) => {
                      await academicoApi.agregarMateria(token, plan.id, datos);
                      setMaterias(await academicoApi.materias(token, plan.id, "admin"));
                    })
                  }
                />
                <ListaMateriasPorAnio
                  materias={materias}
                  acciones={(materia) => (
                    <>
                      <button
                        type="button"
                        className={botonSecundario}
                        aria-label={`Correlatividades de ${materia.nombre}`}
                        onClick={() => {
                          setEditando(null);
                          setCorrelativasDe(materia);
                        }}
                      >
                        Correlatividades
                      </button>
                      {editando?.planMateriaId !== materia.planMateriaId && (
                        <button
                          type="button"
                          className={botonSecundario}
                          onClick={() => {
                            setCorrelativasDe(null);
                            setEditando(materia);
                          }}
                        >
                          Editar
                        </button>
                      )}
                    </>
                  )}
                />
                {correlativasDe && (
                  <EditorCorrelatividades
                    key={correlativasDe.materiaId}
                    planId={plan.id}
                    materia={correlativasDe}
                    materiasDelPlan={materias}
                    onCerrar={() => setCorrelativasDe(null)}
                  />
                )}
                {editando && (
                  <EdicionMateria
                    materia={editando}
                    duracionAnios={carrera.duracionAnios}
                    onCancelar={() => setEditando(null)}
                    onGuardar={(cambios) =>
                      ejecutar(async (token) => {
                        await academicoApi.actualizarMateria(token, plan.id, cambios);
                        setEditando(null);
                        setMaterias(await academicoApi.materias(token, plan.id, "admin"));
                      })
                    }
                  />
                )}
              </>
            )}
          </section>
        ) : (
          <p className="text-zinc-500">Elegí una carrera para ver y editar su plan de estudios.</p>
        )}
      </div>
    </main>
  );
}

function Formulario({ titulo, children, onSubmit }: { titulo: string; children: ReactNode; onSubmit: () => Promise<void> }) {
  const [enviando, setEnviando] = useState(false);
  async function enviar(evento: FormEvent<HTMLFormElement>) {
    evento.preventDefault();
    setEnviando(true);
    await onSubmit();
    setEnviando(false);
  }
  return (
    <form onSubmit={enviar} aria-label={titulo} className="flex flex-col gap-3 rounded-xl border border-zinc-200 p-4 dark:border-zinc-800">
      <h3 className="font-semibold">{titulo}</h3>
      {children}
      <fieldset disabled={enviando} className="contents">
        <button type="submit" className={botonPrimario}>
          Guardar
        </button>
      </fieldset>
    </form>
  );
}

function FormularioCarrera({ onCrear }: { onCrear: (datos: { codigo: string; nombre: string; duracionAnios: number }) => Promise<void> }) {
  const [codigo, setCodigo] = useState("");
  const [nombre, setNombre] = useState("");
  const [duracion, setDuracion] = useState(3);
  return (
    <Formulario
      titulo="Nueva carrera"
      onSubmit={async () => {
        await onCrear({ codigo, nombre, duracionAnios: duracion });
        setCodigo("");
        setNombre("");
      }}
    >
      <label className="flex flex-col gap-1 text-sm font-medium">
        Código
        <input className={campo} value={codigo} onChange={(e) => setCodigo(e.target.value)} required placeholder="TSDS" />
      </label>
      <label className="flex flex-col gap-1 text-sm font-medium">
        Nombre
        <input className={campo} value={nombre} onChange={(e) => setNombre(e.target.value)} required />
      </label>
      <label className="flex flex-col gap-1 text-sm font-medium">
        Duración (años)
        <input className={campo} type="number" min={1} max={6} value={duracion} onChange={(e) => setDuracion(Number(e.target.value))} />
      </label>
    </Formulario>
  );
}

function FormularioPlan({ onCrear }: { onCrear: (datos: { codigoVersion: string; nombre: string }) => Promise<void> }) {
  const [abierto, setAbierto] = useState(false);
  const [codigoVersion, setCodigoVersion] = useState("");
  if (!abierto) {
    return (
      <button type="button" className={botonSecundario} onClick={() => setAbierto(true)}>
        Nuevo plan
      </button>
    );
  }
  return (
    <Formulario
      titulo="Nuevo plan"
      onSubmit={async () => {
        await onCrear({ codigoVersion, nombre: `Plan de estudios ${codigoVersion}` });
        setAbierto(false);
        setCodigoVersion("");
      }}
    >
      <label className="flex flex-col gap-1 text-sm font-medium">
        Resolución
        <input className={campo} value={codigoVersion} onChange={(e) => setCodigoVersion(e.target.value)} required placeholder="Res. 2120/16" />
      </label>
    </Formulario>
  );
}

type DatosMateria = { nombre: string; anio: number; cuatrimestre: Cuatrimestre; cargaHorariaSemanal: number | null };

function CamposMateria({ datos, cambiar, duracionAnios }: { datos: DatosMateria; cambiar: (d: DatosMateria) => void; duracionAnios: number }) {
  return (
    <div className="grid gap-3 sm:grid-cols-[1fr_7rem_10rem_7rem]">
      <label className="flex flex-col gap-1 text-sm font-medium">
        Materia
        <input className={campo} value={datos.nombre} onChange={(e) => cambiar({ ...datos, nombre: e.target.value })} required />
      </label>
      <label className="flex flex-col gap-1 text-sm font-medium">
        Año
        <select className={campo} value={datos.anio} onChange={(e) => cambiar({ ...datos, anio: Number(e.target.value) })}>
          {Array.from({ length: duracionAnios }, (_, i) => i + 1).map((anio) => (
            <option key={anio} value={anio}>
              {anio}.º
            </option>
          ))}
        </select>
      </label>
      <label className="flex flex-col gap-1 text-sm font-medium">
        Cursado
        <select className={campo} value={datos.cuatrimestre} onChange={(e) => cambiar({ ...datos, cuatrimestre: e.target.value as Cuatrimestre })}>
          {(Object.keys(ETIQUETA_CUATRIMESTRE) as Cuatrimestre[]).map((c) => (
            <option key={c} value={c}>
              {ETIQUETA_CUATRIMESTRE[c]}
            </option>
          ))}
        </select>
      </label>
      <label className="flex flex-col gap-1 text-sm font-medium">
        Horas/sem
        <input
          className={campo}
          type="number"
          min={1}
          max={40}
          value={datos.cargaHorariaSemanal ?? ""}
          onChange={(e) => cambiar({ ...datos, cargaHorariaSemanal: e.target.value ? Number(e.target.value) : null })}
        />
      </label>
    </div>
  );
}

function FormularioMateria({ duracionAnios, onAgregar }: { duracionAnios: number; onAgregar: (datos: DatosMateria) => Promise<void> }) {
  const vacia: DatosMateria = { nombre: "", anio: 1, cuatrimestre: "Anual", cargaHorariaSemanal: null };
  const [datos, setDatos] = useState(vacia);
  return (
    <Formulario
      titulo="Agregar materia"
      onSubmit={async () => {
        await onAgregar(datos);
        setDatos({ ...vacia, anio: datos.anio });
      }}
    >
      <CamposMateria datos={datos} cambiar={setDatos} duracionAnios={duracionAnios} />
    </Formulario>
  );
}

function EdicionMateria({
  materia,
  duracionAnios,
  onGuardar,
  onCancelar,
}: {
  materia: MateriaDePlan;
  duracionAnios: number;
  onGuardar: (materia: MateriaDePlan) => Promise<void>;
  onCancelar: () => void;
}) {
  const [datos, setDatos] = useState(materia);
  return (
    <Formulario titulo={`Editar ${materia.codigo}`} onSubmit={() => onGuardar(datos)}>
      <CamposMateria datos={datos} cambiar={(d) => setDatos({ ...datos, ...d })} duracionAnios={duracionAnios} />
      <label className="flex items-center gap-2 text-sm">
        <input type="checkbox" checked={datos.activa} onChange={(e) => setDatos({ ...datos, activa: e.target.checked })} />
        Materia activa
      </label>
      <button type="button" onClick={onCancelar} className={botonSecundario}>
        Cancelar
      </button>
    </Formulario>
  );
}
