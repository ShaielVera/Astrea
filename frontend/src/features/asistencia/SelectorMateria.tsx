"use client";

import Link from "next/link";
import { useEffect, useState, type FormEvent } from "react";
import { OpcionesCarrera } from "@/features/perfil/OpcionesCarrera";
import { useAuth } from "@/features/auth/AuthProvider";
import { mensajeDeError } from "@/features/auth/mensajes-error";
import { academicoApi, NOMBRE_ANIO, type Carrera, type MateriaDePlan } from "@/features/academico/api";

const campo = "w-full min-w-0 rounded-md border border-zinc-300 bg-transparent px-3 py-2 dark:border-zinc-700 dark:bg-zinc-900";

/** Elegir carrera y materia (del plan vigente) y confirmar. Lo usan el estudiante para inscribirse y Secretaría para asignar materias. */
export function SelectorMateria({
  titulo,
  textoBoton,
  onElegir,
  ocupado = false,
  soloMisCarreras,
}: {
  /**
   * Para el estudiante: solo se eligen las carreras de su perfil (null mientras carga). Sin la prop (Secretaría), todas.
   */
  soloMisCarreras?: string[] | null;
  titulo: string;
  textoBoton: string;
  onElegir: (planMateriaId: string) => void | Promise<void>;
  ocupado?: boolean;
}) {
  const { obtenerToken } = useAuth();
  const [carreras, setCarreras] = useState<Carrera[] | null>(null);
  const [carreraId, setCarreraId] = useState("");
  const [materias, setMaterias] = useState<MateriaDePlan[] | null>(null);
  const [planMateriaId, setPlanMateriaId] = useState("");
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
    setMaterias(null);
    setPlanMateriaId("");
    if (!id) return;
    try {
      const token = await obtenerToken();
      const planes = await academicoApi.planes(token, id);
      const vigente = planes.find((plan) => plan.activo) ?? planes[0];
      setMaterias(vigente ? await academicoApi.materias(token, vigente.id) : []);
    } catch (e) {
      setError(mensajeDeError(e));
    }
  }

  function enviar(evento: FormEvent<HTMLFormElement>) {
    evento.preventDefault();
    if (planMateriaId) void onElegir(planMateriaId);
  }

  const disponibles = soloMisCarreras === undefined ? carreras : (carreras?.filter((c) => soloMisCarreras?.includes(c.id)) ?? null);

  if (soloMisCarreras?.length === 0) {
    return (
      <p className="rounded-md bg-amber-50 px-3 py-2 text-sm text-amber-900 dark:bg-amber-950 dark:text-amber-200">
        Para anotarte en tus materias, primero cargá la carrera que cursás en{" "}
        <Link href="/mi-perfil" className="font-semibold underline">
          Mi perfil
        </Link>
        .
      </p>
    );
  }

  const anios = [...new Set((materias ?? []).map((materia) => materia.anio))].sort((a, b) => a - b);

  return (
    <form onSubmit={enviar} aria-label={titulo} className="flex flex-col gap-3 sm:flex-row sm:items-end">
      <label className="flex min-w-0 flex-1 flex-col gap-1 text-sm font-medium">
        Carrera
        <select value={carreraId} onChange={(e) => void elegirCarrera(e.target.value)} disabled={!disponibles || soloMisCarreras === null} className={campo}>
          <option value="">Elegí una carrera</option>
          <OpcionesCarrera carreras={disponibles} mias={null} />
        </select>
      </label>
      <label className="flex min-w-0 flex-1 flex-col gap-1 text-sm font-medium">
        Materia
        <select value={planMateriaId} onChange={(e) => setPlanMateriaId(e.target.value)} disabled={!materias} className={campo}>
          <option value="">{materias ? "Elegí una materia" : "Elegí primero una carrera"}</option>
          {anios.map((anio) => (
            <optgroup key={anio} label={NOMBRE_ANIO[anio] ?? `Año ${anio}`}>
              {materias
                ?.filter((materia) => materia.anio === anio)
                .map((materia) => (
                  <option key={materia.planMateriaId} value={materia.planMateriaId}>
                    {materia.nombre}
                  </option>
                ))}
            </optgroup>
          ))}
        </select>
      </label>
      <button
        type="submit"
        disabled={!planMateriaId || ocupado}
        className="rounded-md bg-indigo-600 px-4 py-2 font-medium text-white hover:bg-indigo-700 disabled:opacity-60"
      >
        {textoBoton}
      </button>
      {error && (
        <p role="alert" className="text-sm text-red-700 dark:text-red-300">
          {error}
        </p>
      )}
    </form>
  );
}
