"use client";

import { useCallback, useEffect, useState } from "react";
import { useAuth } from "@/features/auth/AuthProvider";
import { mensajeDeError } from "@/features/auth/mensajes-error";
import { NOMBRE_ANIO } from "@/features/academico/api";
import { SelectorMateria } from "@/features/asistencia/SelectorMateria";
import { useMisCarreras } from "@/features/perfil/OpcionesCarrera";
import { asistenciaApi, ETIQUETA_VALOR, fechaCorta, type MiAsistencia } from "@/features/asistencia/api";

const ESTILO_VALOR = {
  AsisteRegularmente: "bg-emerald-100 text-emerald-900 dark:bg-emerald-950 dark:text-emerald-200",
  AsisteIrregularmente: "bg-amber-100 text-amber-900 dark:bg-amber-950 dark:text-amber-200",
  NoAsiste: "bg-red-100 text-red-900 dark:bg-red-950 dark:text-red-200",
} as const;

export default function MiCursada() {
  const { obtenerToken } = useAuth();
  const misCarreras = useMisCarreras();
  const [datos, setDatos] = useState<MiAsistencia | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [aviso, setAviso] = useState<string | null>(null);
  const [ocupado, setOcupado] = useState(false);

  const recargar = useCallback(async () => setDatos(await asistenciaApi.miAsistencia(await obtenerToken())), [obtenerToken]);

  useEffect(() => {
    let vigente = true;
    obtenerToken()
      .then((token) => asistenciaApi.miAsistencia(token))
      .then((resultado) => vigente && setDatos(resultado))
      .catch((e) => vigente && setError(mensajeDeError(e)));
    return () => {
      vigente = false;
    };
  }, [obtenerToken]);

  async function inscribirse(planMateriaId: string) {
    setOcupado(true);
    setError(null);
    try {
      const materia = await asistenciaApi.inscribirse(await obtenerToken(), planMateriaId);
      setAviso(`Te anotaste en ${materia.materia}.`);
      await recargar();
    } catch (e) {
      setError(mensajeDeError(e));
    } finally {
      setOcupado(false);
    }
  }

  async function darseDeBaja(planMateriaId: string, materia: string) {
    setError(null);
    try {
      await asistenciaApi.darseDeBaja(await obtenerToken(), planMateriaId);
      setAviso(`Te diste de baja de ${materia}.`);
      await recargar();
    } catch (e) {
      setError(mensajeDeError(e));
    }
  }

  return (
    <main className="mx-auto flex w-full max-w-5xl flex-1 flex-col gap-8 px-4 py-10 sm:px-6">
      <div className="flex flex-col gap-1">
        <h1 className="text-3xl font-semibold tracking-tight">Mi cursada</h1>
        <p className="text-zinc-600 dark:text-zinc-400">
          Anotá las materias que cursás este año: así tus profesores te ven en su lista. Acá también ves lo que confirmaron sobre tu asistencia.
        </p>
      </div>

      <p role="note" className="rounded-xl border border-dorado-500/40 bg-dorado-100/60 px-4 py-3 text-sm text-indigo-950 dark:bg-indigo-900/50 dark:text-indigo-100">
        <span className="font-semibold">Completala, por favor:</span> la dirección usa esta información para saber cuántos estudiantes
        cursan cada materia y para organizar la asistencia. Si no anotás tus materias, tus profesores no te ven en su lista.
      </p>

      {aviso && (
        <p role="status" className="rounded-md bg-emerald-50 px-4 py-2 text-emerald-900 dark:bg-emerald-950 dark:text-emerald-200">
          {aviso}
        </p>
      )}
      {error && (
        <p role="alert" className="rounded-md bg-red-50 px-4 py-2 text-red-800 dark:bg-red-950 dark:text-red-200">
          {error}
        </p>
      )}

      <section aria-labelledby="materias-titulo" className="flex flex-col gap-4">
        <h2 id="materias-titulo" className="text-lg font-semibold">
          Materias que curso {datos ? `en ${datos.cicloLectivo}` : ""}
        </h2>
        <SelectorMateria
          soloMisCarreras={misCarreras}
          yaElegidas={datos?.materias.map((materia) => materia.planMateriaId)}
          titulo="Anotarme en una materia" textoBoton="Anotarme" onElegir={inscribirse} ocupado={ocupado} />
        {!datos ? (
          !error && (
            <p role="status" className="text-zinc-500">
              Cargando…
            </p>
          )
        ) : datos.materias.length === 0 ? (
          <p className="text-zinc-500">Todavía no anotaste ninguna materia.</p>
        ) : (
          <ul aria-label="Materias que curso" className="flex flex-col divide-y divide-zinc-200 rounded-xl border bg-superficie border-zinc-200 dark:divide-zinc-800 dark:border-zinc-800">
            {datos.materias.map((materia) => (
              <li key={materia.planMateriaId} className="flex flex-wrap items-center justify-between gap-3 px-4 py-3">
                <span>
                  <span className="font-medium">{materia.materia}</span>
                  <span className="block text-sm text-zinc-500">
                    {materia.carrera} · {NOMBRE_ANIO[materia.anio] ?? `Año ${materia.anio}`}
                  </span>
                </span>
                <button
                  type="button"
                  onClick={() => void darseDeBaja(materia.planMateriaId, materia.materia)}
                  aria-label={`Darme de baja de ${materia.materia}`}
                  className="rounded-md border border-zinc-300 px-3 py-1.5 text-sm font-medium hover:bg-zinc-100 dark:border-zinc-700 dark:hover:bg-zinc-800"
                >
                  Darme de baja
                </button>
              </li>
            ))}
          </ul>
        )}
      </section>

      <section aria-labelledby="asistencia-titulo" className="flex flex-col gap-3">
        <h2 id="asistencia-titulo" className="text-lg font-semibold">
          Lo que confirmaron tus profesores
        </h2>
        <p className="text-sm text-zinc-600 dark:text-zinc-400">
          Si algo no es correcto o tenés faltas justificadas (por ejemplo, con certificado), hablalo con Secretaría antes del cierre.
        </p>
        {datos && datos.confirmaciones.length === 0 ? (
          <p className="text-zinc-500">Todavía no hay confirmaciones de asistencia.</p>
        ) : (
          <ul aria-label="Confirmaciones de asistencia" className="flex flex-col gap-2">
            {datos?.confirmaciones.map((confirmacion) => (
              <li key={`${confirmacion.relevamientoId}-${confirmacion.materia}`} className="rounded-xl border bg-superficie border-zinc-200 px-4 py-3 dark:border-zinc-800">
                <div className="flex flex-wrap items-center justify-between gap-2">
                  <span className="font-medium">{confirmacion.materia}</span>
                  <span className={`rounded-full px-2.5 py-0.5 text-sm font-medium ${ESTILO_VALOR[confirmacion.valor]}`}>
                    {ETIQUETA_VALOR[confirmacion.valor]}
                  </span>
                </div>
                <p className="text-sm text-zinc-500">
                  {confirmacion.relevamiento} · cierra el {fechaCorta(confirmacion.fechaCierre)}
                </p>
                {confirmacion.observacion && <p className="mt-1 text-sm">Observación: {confirmacion.observacion}</p>}
              </li>
            ))}
          </ul>
        )}
      </section>
    </main>
  );
}
