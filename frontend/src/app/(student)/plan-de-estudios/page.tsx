"use client";

import { useEffect, useState } from "react";
import { useAuth } from "@/features/auth/AuthProvider";
import { mensajeDeError } from "@/features/auth/mensajes-error";
import { academicoApi, type Carrera, type MateriaDePlan, type PlanEstudio } from "@/features/academico/api";
import { ListaMateriasPorAnio } from "@/features/academico/ListaMateriasPorAnio";

export default function PlanDeEstudiosPage() {
  const { obtenerToken } = useAuth();
  const [carreras, setCarreras] = useState<Carrera[] | null>(null);
  const [carreraId, setCarreraId] = useState("");
  const [plan, setPlan] = useState<PlanEstudio | null>(null);
  const [materias, setMaterias] = useState<MateriaDePlan[] | null>(null);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    obtenerToken()
      .then((token) => academicoApi.carreras(token))
      .then(setCarreras)
      .catch((e) => setError(mensajeDeError(e)));
  }, [obtenerToken]);

  async function elegirCarrera(id: string) {
    setCarreraId(id);
    setPlan(null);
    setMaterias(null);
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

  const carrera = carreras?.find((c) => c.id === carreraId);

  return (
    <main className="mx-auto flex w-full max-w-3xl flex-1 flex-col gap-6 px-4 py-10 sm:px-6">
      <div className="flex flex-col gap-1">
        <h1 className="text-3xl font-semibold tracking-tight">Plan de estudios</h1>
        <p className="text-zinc-600 dark:text-zinc-400">Elegí una carrera para ver sus materias año por año.</p>
      </div>

      <label className="flex flex-col gap-1 text-sm font-medium">
        Carrera
        <select
          value={carreraId}
          onChange={(e) => void elegirCarrera(e.target.value)}
          disabled={!carreras}
          className="rounded-md border border-zinc-300 bg-transparent px-3 py-2 font-normal dark:border-zinc-700 dark:bg-zinc-900"
        >
          <option value="">{carreras ? "Seleccioná una carrera" : "Cargando carreras…"}</option>
          {carreras?.map((c) => (
            <option key={c.id} value={c.id}>
              {c.nombre}
            </option>
          ))}
        </select>
      </label>

      {error && (
        <p role="alert" className="text-red-700 dark:text-red-300">
          {error}
        </p>
      )}

      {carrera && plan && (
        <p className="text-sm text-zinc-600 dark:text-zinc-400" data-testid="plan-vigente">
          {carrera.codigo} · {plan.nombre} · {carrera.duracionAnios} años
        </p>
      )}

      {carreraId && !materias && !error && (
        <p role="status" className="text-zinc-500">
          Cargando materias…
        </p>
      )}
      {materias && <ListaMateriasPorAnio materias={materias} />}
    </main>
  );
}
