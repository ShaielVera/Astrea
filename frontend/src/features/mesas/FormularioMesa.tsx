"use client";

import { useEffect, useState, type FormEvent } from "react";
import { useAuth } from "@/features/auth/AuthProvider";
import { mensajeDeError } from "@/features/auth/mensajes-error";
import { academicoApi, type Carrera, type MateriaDePlan, type PlanEstudio } from "@/features/academico/api";
import { horaCorta, type DatosMesa, type MesaExamen } from "./api";

const campo = "w-full min-w-0 rounded-md border border-zinc-300 bg-transparent px-3 py-2 font-normal dark:border-zinc-700 dark:bg-zinc-900";
const botonPrimario = "rounded-md bg-indigo-600 px-4 py-2 font-medium text-white hover:bg-indigo-700 disabled:opacity-60";
const botonSecundario =
  "rounded-md border border-zinc-300 px-4 py-2 font-medium hover:bg-zinc-100 dark:border-zinc-700 dark:hover:bg-zinc-800";

type Props = {
  carreras: Carrera[];
  /** Mesa a corregir; sin ella, el formulario da de alta una nueva. */
  mesa?: MesaExamen;
  onGuardar: (datos: DatosMesa) => Promise<void>;
  onCancelar?: () => void;
};

/** Alta o corrección de una mesa. La materia se elige por carrera → plan → materia. */
export function FormularioMesa({ carreras, mesa, onGuardar, onCancelar }: Props) {
  const { obtenerToken } = useAuth();
  const [carreraId, setCarreraId] = useState(mesa?.carreraId ?? "");
  const [planes, setPlanes] = useState<PlanEstudio[]>([]);
  const [planId, setPlanId] = useState(mesa?.planId ?? "");
  const [materias, setMaterias] = useState<MateriaDePlan[]>([]);
  const [materiaId, setMateriaId] = useState(mesa?.materiaId ?? "");
  const [turno, setTurno] = useState(mesa?.turno ?? "");
  const [llamado, setLlamado] = useState(String(mesa?.llamado ?? 1));
  const [fecha, setFecha] = useState(mesa?.fecha ?? "");
  const [hora, setHora] = useState(mesa ? horaCorta(mesa.hora) : "");
  const [aula, setAula] = useState(mesa?.aula ?? "");
  const [tribunal, setTribunal] = useState(mesa?.tribunal.join("\n") ?? "");
  const [inscripcionDesde, setInscripcionDesde] = useState(mesa?.inscripcionDesde ?? "");
  const [inscripcionHasta, setInscripcionHasta] = useState(mesa?.inscripcionHasta ?? "");
  const [observaciones, setObservaciones] = useState(mesa?.observaciones ?? "");
  const [enviando, setEnviando] = useState(false);
  const [error, setError] = useState<string | null>(null);

  // Planes de la carrera elegida (incluye inactivos: puede haber mesas de planes que ya no se dictan).
  useEffect(() => {
    if (!carreraId) return;
    let vigente = true;
    obtenerToken()
      .then((token) => academicoApi.planes(token, carreraId, "admin"))
      .then((lista) => {
        if (!vigente) return;
        setPlanes(lista);
        setPlanId((actual) => (lista.some((p) => p.id === actual) ? actual : (lista[0]?.id ?? "")));
      })
      .catch((e) => vigente && setError(mensajeDeError(e)));
    return () => {
      vigente = false;
    };
  }, [carreraId, obtenerToken]);

  useEffect(() => {
    if (!planId) return;
    let vigente = true;
    obtenerToken()
      .then((token) => academicoApi.materias(token, planId, "admin"))
      .then((lista) => vigente && setMaterias(lista))
      .catch((e) => vigente && setError(mensajeDeError(e)));
    return () => {
      vigente = false;
    };
  }, [planId, obtenerToken]);

  function elegirCarrera(id: string) {
    setCarreraId(id);
    setPlanes([]);
    setPlanId("");
    setMaterias([]);
    setMateriaId("");
  }

  async function enviar(evento: FormEvent<HTMLFormElement>) {
    evento.preventDefault();
    setEnviando(true);
    setError(null);
    try {
      await onGuardar({
        planId,
        materiaId,
        turno,
        llamado: Number(llamado),
        fecha,
        hora: `${hora}:00`,
        aula: aula.trim() || null,
        tribunal: tribunal.split("\n").map((integrante) => integrante.trim()).filter(Boolean),
        inscripcionDesde: inscripcionDesde || null,
        inscripcionHasta: inscripcionHasta || null,
        observaciones: observaciones.trim() || null,
      });
    } catch (e) {
      setError(mensajeDeError(e));
    } finally {
      setEnviando(false);
    }
  }

  const titulo = mesa ? `Editar mesa de ${mesa.materiaNombre}` : "Nueva mesa";

  return (
    <form onSubmit={enviar} aria-label={titulo} className="flex flex-col gap-4 rounded-xl border bg-superficie border-zinc-200 p-4 dark:border-zinc-800">
      <h2 className="text-lg font-semibold">{titulo}</h2>

      {error && (
        <p role="alert" className="rounded-md bg-red-50 px-3 py-2 text-sm text-red-700 dark:bg-red-950 dark:text-red-300">
          {error}
        </p>
      )}

      <div className="grid grid-cols-1 gap-3 sm:grid-cols-3">
        <label className="flex min-w-0 flex-col gap-1 text-sm font-medium">
          Carrera
          <select value={carreraId} onChange={(e) => elegirCarrera(e.target.value)} required className={campo}>
            <option value="">Elegí una carrera</option>
            {carreras.map((c) => (
              <option key={c.id} value={c.id}>
                {c.nombre}
              </option>
            ))}
          </select>
        </label>
        <label className="flex min-w-0 flex-col gap-1 text-sm font-medium">
          Plan
          <select value={planId} onChange={(e) => setPlanId(e.target.value)} required disabled={planes.length === 0} className={campo}>
            {planes.length === 0 && <option value="">—</option>}
            {planes.map((p) => (
              <option key={p.id} value={p.id}>
                {p.nombre}
                {p.activo ? "" : " (inactivo)"}
              </option>
            ))}
          </select>
        </label>
        <label className="flex min-w-0 flex-col gap-1 text-sm font-medium">
          Materia
          <select value={materiaId} onChange={(e) => setMateriaId(e.target.value)} required disabled={materias.length === 0} className={campo}>
            <option value="">Elegí una materia</option>
            {materias.map((m) => (
              <option key={m.materiaId} value={m.materiaId}>
                {m.anio}.º · {m.nombre}
              </option>
            ))}
          </select>
        </label>
      </div>

      <div className="grid grid-cols-1 gap-3 sm:grid-cols-[1fr_6rem_10rem_8rem_1fr]">
        <label className="flex min-w-0 flex-col gap-1 text-sm font-medium">
          Turno
          <input value={turno} onChange={(e) => setTurno(e.target.value)} required maxLength={60} placeholder="Ej.: Diciembre 2026" className={campo} />
        </label>
        <label className="flex min-w-0 flex-col gap-1 text-sm font-medium">
          Llamado
          <select value={llamado} onChange={(e) => setLlamado(e.target.value)} className={campo}>
            {[1, 2, 3, 4, 5].map((n) => (
              <option key={n} value={n}>
                {n}.º
              </option>
            ))}
          </select>
        </label>
        <label className="flex min-w-0 flex-col gap-1 text-sm font-medium">
          Fecha
          <input type="date" value={fecha} onChange={(e) => setFecha(e.target.value)} required className={campo} />
        </label>
        <label className="flex min-w-0 flex-col gap-1 text-sm font-medium">
          Hora
          <input type="time" value={hora} onChange={(e) => setHora(e.target.value)} required className={campo} />
        </label>
        <label className="flex min-w-0 flex-col gap-1 text-sm font-medium">
          Aula
          <input value={aula} onChange={(e) => setAula(e.target.value)} maxLength={50} className={campo} />
        </label>
      </div>

      <div className="grid grid-cols-1 gap-3 sm:grid-cols-2">
        <label className="flex min-w-0 flex-col gap-1 text-sm font-medium">
          Profesores (tribunal)
          <textarea
            value={tribunal}
            onChange={(e) => setTribunal(e.target.value)}
            required
            rows={3}
            aria-describedby="ayuda-tribunal"
            className={campo}
          />
          <span id="ayuda-tribunal" className="text-xs font-normal text-zinc-500">
            Un profesor por renglón. El primero preside la mesa.
          </span>
        </label>
        <label className="flex min-w-0 flex-col gap-1 text-sm font-medium">
          Observaciones
          <textarea value={observaciones} onChange={(e) => setObservaciones(e.target.value)} rows={3} maxLength={500} className={campo} />
        </label>
      </div>

      <fieldset className="grid grid-cols-1 gap-3 sm:grid-cols-2">
        <legend className="mb-1 text-sm font-medium">Inscripción (opcional)</legend>
        <label className="flex min-w-0 flex-col gap-1 text-sm font-medium">
          Abre
          <input type="date" value={inscripcionDesde} onChange={(e) => setInscripcionDesde(e.target.value)} className={campo} />
        </label>
        <label className="flex min-w-0 flex-col gap-1 text-sm font-medium">
          Cierra
          <input type="date" value={inscripcionHasta} onChange={(e) => setInscripcionHasta(e.target.value)} className={campo} />
        </label>
      </fieldset>

      <div className="flex flex-wrap gap-3">
        <button type="submit" className={botonPrimario} disabled={enviando}>
          {mesa ? "Guardar cambios" : "Guardar borrador"}
        </button>
        {onCancelar && (
          <button type="button" className={botonSecundario} onClick={onCancelar}>
            Descartar
          </button>
        )}
      </div>
    </form>
  );
}
