"use client";

import { useEffect, useState } from "react";
import { useAuth } from "@/features/auth/AuthProvider";
import { mensajeDeError } from "@/features/auth/mensajes-error";
import {
  academicoApi,
  agruparPorAnio,
  NOMBRE_ANIO,
  type Carrera,
  type CorrelatividadesDeMateria,
  type EstadoMateria,
  type MateriaDePlan,
  type PlanEstudio,
  type Requisito,
  type TipoCorrelatividad,
  type Verificacion,
} from "@/features/academico/api";
import { RequisitosCorrelatividad } from "@/features/academico/RequisitosCorrelatividad";
import { OpcionesCarrera, useMisCarreras } from "@/features/perfil/OpcionesCarrera";

const campo = "rounded-md border border-zinc-300 bg-transparent px-3 py-2 font-normal dark:border-zinc-700 dark:bg-zinc-900";
const botonSecundario =
  "rounded-md border border-zinc-300 px-3 py-1.5 text-sm font-medium hover:bg-zinc-100 disabled:opacity-60 dark:border-zinc-700 dark:hover:bg-zinc-800";

const ETIQUETA_ESTADO: Record<EstadoMateria, string> = {
  SinCursar: "Sin cursar",
  Regularizada: "Regularizada",
  Aprobada: "Aprobada",
};

export default function CorrelatividadesPage() {
  const { obtenerToken } = useAuth();
  const misCarreras = useMisCarreras();
  const [carreras, setCarreras] = useState<Carrera[] | null>(null);
  const [carreraId, setCarreraId] = useState("");
  const [plan, setPlan] = useState<PlanEstudio | null>(null);
  const [materias, setMaterias] = useState<MateriaDePlan[] | null>(null);
  const [anio, setAnio] = useState(1);
  const [recorrido, setRecorrido] = useState<string[]>([]);
  const [detalle, setDetalle] = useState<CorrelatividadesDeMateria | null>(null);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    let vigente = true;
    obtenerToken()
      .then((token) => academicoApi.carreras(token))
      .then((lista) => vigente && setCarreras(lista))
      .catch((e) => vigente && setError(mensajeDeError(e)));
    return () => {
      vigente = false;
    };
  }, [obtenerToken]);

  async function elegirCarrera(id: string) {
    setCarreraId(id);
    setPlan(null);
    setMaterias(null);
    setDetalle(null);
    setRecorrido([]);
    setAnio(1);
    setError(null);
    if (!id) return;
    try {
      const token = await obtenerToken();
      const [vigente] = await academicoApi.planes(token, id);
      if (!vigente) {
        setMaterias([]);
        return;
      }
      setPlan(vigente);
      setMaterias(await academicoApi.materias(token, vigente.id));
    } catch (e) {
      setError(mensajeDeError(e));
    }
  }

  /** Muestra las correlatividades de una materia; `recorrido` guarda el camino para poder volver. */
  async function verMateria(materiaId: string, nuevoRecorrido: string[]) {
    if (!plan) return;
    setError(null);
    try {
      const token = await obtenerToken();
      setDetalle(await academicoApi.correlatividades(token, plan.id, materiaId));
      setRecorrido(nuevoRecorrido);
    } catch (e) {
      setError(mensajeDeError(e));
    }
  }

  const delAnio = materias ? (agruparPorAnio(materias).find(([a]) => a === anio)?.[1] ?? []) : [];
  const anios = materias ? agruparPorAnio(materias).map(([a]) => a) : [];

  return (
    <main className="mx-auto flex w-full max-w-5xl flex-1 flex-col gap-6 px-4 py-10 sm:px-6">
      <div className="flex flex-col gap-1">
        <h1 className="text-3xl font-semibold tracking-tight">Correlatividades</h1>
        <p className="text-zinc-600 dark:text-zinc-400">
          Elegí tu carrera y una materia para ver qué necesitás tener regularizado o aprobado para cursarla y para rendirla.
        </p>
      </div>

      <div className="flex flex-wrap gap-4">
        <label className="flex min-w-64 flex-1 flex-col gap-1 text-sm font-medium">
          Carrera
          <select value={carreraId} onChange={(e) => void elegirCarrera(e.target.value)} disabled={!carreras} className={campo}>
            <option value="">{carreras ? "Seleccioná una carrera" : "Cargando carreras…"}</option>
            <OpcionesCarrera carreras={carreras} mias={misCarreras} />
          </select>
        </label>
        {anios.length > 0 && (
          <label className="flex flex-col gap-1 text-sm font-medium">
            Año
            <select value={anio} onChange={(e) => setAnio(Number(e.target.value))} className={campo}>
              {anios.map((a) => (
                <option key={a} value={a}>
                  {NOMBRE_ANIO[a] ?? `Año ${a}`}
                </option>
              ))}
            </select>
          </label>
        )}
      </div>

      {error && (
        <p role="alert" className="text-red-700 dark:text-red-300">
          {error}
        </p>
      )}

      {carreraId && !materias && !error && (
        <p role="status" className="text-zinc-500">
          Cargando materias…
        </p>
      )}

      {materias && (
        <div className="grid gap-6 lg:grid-cols-[16rem_1fr]">
          <nav aria-label="Lista de materias" className="self-start rounded-xl border border-zinc-200 bg-superficie p-2 shadow-sm dark:border-zinc-800">
            {delAnio.length > 0 && (
              <p className="border-b border-zinc-200 px-3 pt-1 pb-2 text-xs font-semibold tracking-wide text-zinc-500 uppercase dark:border-zinc-800">
                {NOMBRE_ANIO[anio] ?? `Año ${anio}`} · {delAnio.length} {delAnio.length === 1 ? "materia" : "materias"}
              </p>
            )}
            <ul className="flex flex-col divide-y divide-zinc-200 dark:divide-zinc-800">
              {delAnio.map((materia) => (
                <li key={materia.planMateriaId} className="py-1">
                  <button
                    type="button"
                    onClick={() => void verMateria(materia.materiaId, [materia.materiaId])}
                    aria-current={detalle?.materiaId === materia.materiaId}
                    className={`w-full rounded-md px-3 py-2 text-left text-sm hover:bg-zinc-100 dark:hover:bg-zinc-800 ${
                      detalle?.materiaId === materia.materiaId ? "bg-indigo-50 font-medium dark:bg-indigo-950" : ""
                    }`}
                  >
                    {materia.nombre}
                  </button>
                </li>
              ))}
            </ul>
            {materias.length === 0 && <p className="text-zinc-500">Esta carrera todavía no tiene materias cargadas.</p>}
          </nav>

          {detalle ? (
            <section aria-labelledby="materia-titulo" className="flex min-w-0 flex-col gap-4 rounded-xl border border-zinc-200 bg-superficie p-5 shadow-sm dark:border-zinc-800">
              <div className="flex flex-wrap items-start justify-between gap-3">
                <div>
                  <h2 id="materia-titulo" className="text-xl font-semibold">
                    {detalle.nombre}
                  </h2>
                  <p className="text-sm text-zinc-500">
                    {detalle.codigo} · {NOMBRE_ANIO[detalle.anio] ?? `Año ${detalle.anio}`}
                  </p>
                </div>
                {recorrido.length > 1 && (
                  <button type="button" className={botonSecundario} onClick={() => void verMateria(recorrido.at(-2)!, recorrido.slice(0, -1))}>
                    Volver
                  </button>
                )}
              </div>
              <RequisitosCorrelatividad detalle={detalle} onElegirMateria={(id) => void verMateria(id, [...recorrido, id])} />
              {/* La key reinicia la simulación al cambiar de materia. */}
              <VerificarSituacion key={detalle.materiaId} detalle={detalle} />
            </section>
          ) : (
            <p className="self-start rounded-xl border border-dashed border-zinc-300 bg-superficie/60 p-5 text-zinc-500 dark:border-zinc-700">Elegí una materia para ver sus correlatividades.</p>
          )}
        </div>
      )}
    </main>
  );
}

/** El estudiante declara cómo tiene cada requisito y Astrea le dice si puede cursar o rendir. No guarda nada. */
function VerificarSituacion({ detalle }: { detalle: CorrelatividadesDeMateria }) {
  const { obtenerToken } = useAuth();
  const [estados, setEstados] = useState<Record<string, EstadoMateria>>({});
  const [verificacion, setVerificacion] = useState<Verificacion | null>(null);
  const [verificando, setVerificando] = useState(false);
  const [error, setError] = useState<string | null>(null);

  // Materias que aparecen como requisito para cursar o para rendir, sin repetir.
  const requisitos = [...detalle.paraCursar, ...detalle.paraRendir].filter(
    (r, i, todos): r is Requisito & { materiaId: string } => r.materiaId !== null && todos.findIndex((o) => o.materiaId === r.materiaId) === i,
  );

  if (detalle.paraCursar.length + detalle.paraRendir.length === 0) return null;

  async function verificar(tipo: TipoCorrelatividad) {
    setVerificando(true);
    setError(null);
    try {
      const token = await obtenerToken();
      const lista = Object.entries(estados).map(([materiaId, estado]) => ({ materiaId, estado }));
      setVerificacion(await academicoApi.verificarCorrelatividades(token, detalle.planId, detalle.materiaId, tipo, lista));
    } catch (e) {
      setError(mensajeDeError(e));
    } finally {
      setVerificando(false);
    }
  }

  return (
    <section aria-labelledby="verificar-titulo" className="flex flex-col gap-3 rounded-xl bg-indigo-50/60 p-4 dark:bg-indigo-950/50">
      <div>
        <h3 id="verificar-titulo" className="font-semibold">
          ¿Puedo cursarla o rendirla?
        </h3>
        <p className="text-xs text-zinc-500">Indicá cómo tenés cada materia. Es una simulación: Astrea no guarda estos datos.</p>
      </div>
      {requisitos.length > 0 && (
        <ul className="grid gap-2 sm:grid-cols-2">
          {requisitos.map((requisito) => (
            <li key={requisito.materiaId}>
              <label className="flex flex-col gap-1 text-sm">
                {requisito.nombre}
                <select
                  className={campo}
                  value={estados[requisito.materiaId] ?? "SinCursar"}
                  onChange={(e) => {
                    setEstados({ ...estados, [requisito.materiaId]: e.target.value as EstadoMateria });
                    setVerificacion(null);
                  }}
                >
                  {(Object.keys(ETIQUETA_ESTADO) as EstadoMateria[]).map((estado) => (
                    <option key={estado} value={estado}>
                      {ETIQUETA_ESTADO[estado]}
                    </option>
                  ))}
                </select>
              </label>
            </li>
          ))}
        </ul>
      )}
      <div className="flex flex-wrap gap-2">
        <button type="button" className={botonSecundario} disabled={verificando} onClick={() => void verificar("Cursar")}>
          Verificar para cursar
        </button>
        <button type="button" className={botonSecundario} disabled={verificando} onClick={() => void verificar("Rendir")}>
          Verificar para rendir
        </button>
      </div>
      {error && (
        <p role="alert" className="text-sm text-red-700 dark:text-red-300">
          {error}
        </p>
      )}
      {verificacion && <ResultadoVerificacion verificacion={verificacion} />}
    </section>
  );
}

function ResultadoVerificacion({ verificacion }: { verificacion: Verificacion }) {
  const accion = verificacion.tipo === "Cursar" ? "cursarla" : "rendirla";
  const nombre = (r: Requisito) => r.nombre ?? r.descripcion;

  if (verificacion.resultado === "Cumple") {
    return (
      <p role="status" className="rounded-md bg-emerald-100 px-3 py-2 text-sm text-emerald-900 dark:bg-emerald-950 dark:text-emerald-200">
        Cumplís las correlatividades para {accion}.
      </p>
    );
  }

  if (verificacion.resultado === "RequiereVerificacion") {
    return (
      <div role="status" className="rounded-md bg-sky-100 px-3 py-2 text-sm text-sky-900 dark:bg-sky-950 dark:text-sky-200">
        <p>Cumplís las materias pedidas para {accion}. La Secretaría tiene que verificar además:</p>
        <ul className="list-disc pl-5">
          {verificacion.aVerificar.map((r) => (
            <li key={r.id}>{nombre(r)}</li>
          ))}
        </ul>
      </div>
    );
  }

  // Los obligatorios se listan uno por uno; cada grupo de alternativas, en una sola línea.
  const texto = (r: Requisito) => `${nombre(r)} (${r.condicion.toLowerCase()})`;
  const lineas: { clave: string; texto: string }[] = [];
  for (const r of verificacion.faltantes) {
    if (r.grupoAlternativa === null) {
      lineas.push({ clave: r.id, texto: texto(r) });
      continue;
    }
    const clave = `grupo-${r.grupoAlternativa}`;
    const existente = lineas.find((l) => l.clave === clave);
    if (existente) existente.texto += ` o ${texto(r)}`;
    else lineas.push({ clave, texto: `Una de estas: ${texto(r)}` });
  }

  return (
    <div role="status" className="rounded-md bg-red-50 px-3 py-2 text-sm text-red-800 dark:bg-red-950 dark:text-red-200">
      <p>Todavía no podés {accion}. Te falta:</p>
      <ul className="list-disc pl-5">
        {lineas.map((linea) => (
          <li key={linea.clave}>{linea.texto}</li>
        ))}
      </ul>
    </div>
  );
}
