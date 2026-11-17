"use client";

import { useEffect, useRef, useState, type FormEvent } from "react";
import { useAuth } from "@/features/auth/AuthProvider";
import { mensajeDeError } from "@/features/auth/mensajes-error";
import {
  academicoApi,
  type CondicionRequisito,
  type CorrelatividadesDeMateria,
  type MateriaDePlan,
  type TipoCorrelatividad,
} from "./api";
import { RequisitosCorrelatividad } from "./RequisitosCorrelatividad";

const campo = "rounded-md border border-zinc-300 bg-transparent px-3 py-2 dark:border-zinc-700 dark:bg-zinc-900";
const botonPrimario = "rounded-md bg-indigo-600 px-4 py-2 font-medium text-white hover:bg-indigo-700 disabled:opacity-60";
const botonSecundario =
  "rounded-md border border-zinc-300 px-3 py-1.5 text-sm font-medium hover:bg-zinc-100 dark:border-zinc-700 dark:hover:bg-zinc-800";

/** Valor del select de requisito que indica "condición en texto, sin materia". */
const SIN_MATERIA = "texto";

type Props = {
  planId: string;
  materia: MateriaDePlan;
  materiasDelPlan: MateriaDePlan[];
  onCerrar: () => void;
};

/** Panel de administración para ver, agregar y quitar correlatividades de una materia. */
export function EditorCorrelatividades({ planId, materia, materiasDelPlan, onCerrar }: Props) {
  const { obtenerToken } = useAuth();
  const [detalle, setDetalle] = useState<CorrelatividadesDeMateria | null>(null);
  const [error, setError] = useState<string | null>(null);
  const panel = useRef<HTMLElement>(null);

  // El panel aparece debajo de la lista de materias: lo traemos a la vista al abrirlo.
  useEffect(() => {
    panel.current?.scrollIntoView({ behavior: "smooth", block: "start" });
  }, []);

  useEffect(() => {
    let vigente = true;
    obtenerToken()
      .then((token) => academicoApi.correlatividades(token, planId, materia.materiaId, "admin"))
      .then((d) => vigente && setDetalle(d))
      .catch((e) => vigente && setError(mensajeDeError(e)));
    return () => {
      vigente = false;
    };
  }, [obtenerToken, planId, materia.materiaId]);

  async function ejecutar(accion: (token: string) => Promise<void>) {
    setError(null);
    try {
      await accion(await obtenerToken());
    } catch (e) {
      setError(mensajeDeError(e));
    }
  }

  return (
    <section ref={panel} aria-label={`Correlatividades de ${materia.nombre}`} className="flex flex-col gap-4 rounded-xl border bg-superficie border-indigo-200 p-4 dark:border-indigo-900">
      <div className="flex flex-wrap items-start justify-between gap-2">
        <div>
          <h3 className="font-semibold">Correlatividades de {materia.nombre}</h3>
          <p className="text-xs text-zinc-500">Cargalas tal como figuran en el régimen de correlatividades aprobado.</p>
        </div>
        <button type="button" className={botonSecundario} onClick={onCerrar}>
          Cerrar
        </button>
      </div>

      {error && (
        <p role="alert" className="rounded-md bg-red-50 px-3 py-2 text-sm text-red-700 dark:bg-red-950 dark:text-red-300">
          {error}
        </p>
      )}

      {detalle ? (
        <RequisitosCorrelatividad
          detalle={detalle}
          acciones={(requisito) => (
            <button
              type="button"
              className="text-xs font-medium text-red-700 hover:underline dark:text-red-300"
              aria-label={`Quitar ${requisito.nombre ?? requisito.descripcion}`}
              onClick={() =>
                ejecutar(async (token) => {
                  await academicoApi.quitarCorrelatividad(token, planId, requisito.id);
                  setDetalle(await academicoApi.correlatividades(token, planId, materia.materiaId, "admin"));
                })
              }
            >
              Quitar
            </button>
          )}
        />
      ) : (
        !error && (
          <p role="status" className="text-sm text-zinc-500">
            Cargando correlatividades…
          </p>
        )
      )}

      <FormularioRequisito
        materia={materia}
        materiasDelPlan={materiasDelPlan}
        onAgregar={(datos) =>
          ejecutar(async (token) => {
            setDetalle(await academicoApi.agregarCorrelatividad(token, planId, { materiaDestinoId: materia.materiaId, ...datos }));
          })
        }
      />
    </section>
  );
}

type DatosRequisito = {
  tipo: TipoCorrelatividad;
  materiaRequisitoId: string | null;
  condicion: CondicionRequisito;
  grupoAlternativa: number | null;
  descripcion: string | null;
};

function FormularioRequisito({
  materia,
  materiasDelPlan,
  onAgregar,
}: {
  materia: MateriaDePlan;
  materiasDelPlan: MateriaDePlan[];
  onAgregar: (datos: DatosRequisito) => Promise<void>;
}) {
  const [tipo, setTipo] = useState<TipoCorrelatividad>("Cursar");
  const [requisito, setRequisito] = useState("");
  const [condicion, setCondicion] = useState<CondicionRequisito>("Regularizada");
  const [grupo, setGrupo] = useState("");
  const [descripcion, setDescripcion] = useState("");
  const [enviando, setEnviando] = useState(false);

  const candidatas = materiasDelPlan.filter((m) => m.materiaId !== materia.materiaId);
  const sinMateria = requisito === SIN_MATERIA;

  async function enviar(evento: FormEvent<HTMLFormElement>) {
    evento.preventDefault();
    setEnviando(true);
    await onAgregar({
      tipo,
      materiaRequisitoId: sinMateria ? null : requisito,
      condicion,
      grupoAlternativa: grupo ? Number(grupo) : null,
      descripcion: descripcion.trim() || null,
    });
    setEnviando(false);
    setRequisito("");
    setDescripcion("");
  }

  return (
    <form onSubmit={enviar} aria-label="Agregar requisito" className="flex flex-col gap-3 rounded-lg bg-zinc-50 p-3 dark:bg-zinc-900">
      <h4 className="text-sm font-semibold">Agregar requisito</h4>
      <div className="grid gap-3 sm:grid-cols-[8rem_1fr_9rem]">
        <label className="flex flex-col gap-1 text-sm font-medium">
          Para
          <select className={campo} value={tipo} onChange={(e) => setTipo(e.target.value as TipoCorrelatividad)}>
            <option value="Cursar">Cursar</option>
            <option value="Rendir">Rendir</option>
          </select>
        </label>
        <label className="flex flex-col gap-1 text-sm font-medium">
          Requisito
          <select className={campo} value={requisito} onChange={(e) => setRequisito(e.target.value)} required>
            <option value="">Elegí una materia</option>
            {candidatas.map((m) => (
              <option key={m.materiaId} value={m.materiaId}>
                {m.anio}.º · {m.nombre}
              </option>
            ))}
            <option value={SIN_MATERIA}>Otro requisito (texto)</option>
          </select>
        </label>
        <label className="flex flex-col gap-1 text-sm font-medium">
          Condición
          <select className={campo} value={condicion} onChange={(e) => setCondicion(e.target.value as CondicionRequisito)}>
            <option value="Regularizada">Regularizada</option>
            <option value="Aprobada">Aprobada</option>
          </select>
        </label>
      </div>
      <div className="grid gap-3 sm:grid-cols-[1fr_9rem]">
        <label className="flex flex-col gap-1 text-sm font-medium">
          {sinMateria ? "Texto del requisito" : "Observación (opcional)"}
          <input className={campo} value={descripcion} onChange={(e) => setDescripcion(e.target.value)} required={sinMateria} maxLength={300} />
        </label>
        <label className="flex flex-col gap-1 text-sm font-medium">
          Grupo alternativo
          <input
            className={campo}
            type="number"
            min={1}
            max={20}
            value={grupo}
            onChange={(e) => setGrupo(e.target.value)}
            aria-describedby="ayuda-grupo"
          />
        </label>
      </div>
      <p id="ayuda-grupo" className="text-xs text-zinc-500">
        Dejá el grupo vacío si el requisito es obligatorio. Usá el mismo número en varios requisitos cuando alcanza con cumplir uno de ellos (&quot;y/o&quot;).
      </p>
      <fieldset disabled={enviando} className="contents">
        <button type="submit" className={botonPrimario}>
          Agregar
        </button>
      </fieldset>
    </form>
  );
}
