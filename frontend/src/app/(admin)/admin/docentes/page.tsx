"use client";

import Link from "next/link";
import { useCallback, useEffect, useState, type FormEvent } from "react";
import { useAuth } from "@/features/auth/AuthProvider";
import { mensajeDeError } from "@/features/auth/mensajes-error";
import { NOMBRE_ANIO } from "@/features/academico/api";
import { SelectorMateria } from "@/features/asistencia/SelectorMateria";
import { asistenciaApi, type Docente, type UsuarioEncontrado } from "@/features/asistencia/api";

const botonChico =
  "rounded-md border border-zinc-300 px-3 py-1.5 text-sm font-medium hover:bg-zinc-100 disabled:opacity-60 dark:border-zinc-700 dark:hover:bg-zinc-800";

export default function Docentes() {
  const { obtenerToken } = useAuth();
  const [docentes, setDocentes] = useState<Docente[] | null>(null);
  const [texto, setTexto] = useState("");
  const [candidatos, setCandidatos] = useState<UsuarioEncontrado[] | null>(null);
  const [aviso, setAviso] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [quitando, setQuitando] = useState<string | null>(null);

  const recargar = useCallback(async () => setDocentes(await asistenciaApi.docentes(await obtenerToken())), [obtenerToken]);

  useEffect(() => {
    let vigente = true;
    obtenerToken()
      .then((token) => asistenciaApi.docentes(token))
      .then((lista) => vigente && setDocentes(lista))
      .catch((e) => vigente && setError(mensajeDeError(e)));
    return () => {
      vigente = false;
    };
  }, [obtenerToken]);

  async function ejecutar(accion: () => Promise<unknown>, mensaje: string) {
    setError(null);
    setAviso(null);
    try {
      await accion();
      setAviso(mensaje);
      await recargar();
    } catch (e) {
      setError(mensajeDeError(e));
    }
  }

  async function buscar(evento: FormEvent<HTMLFormElement>) {
    evento.preventDefault();
    setError(null);
    try {
      setCandidatos(await asistenciaApi.candidatos(await obtenerToken(), texto.trim()));
    } catch (e) {
      setError(mensajeDeError(e));
    }
  }

  return (
    <main className="mx-auto flex w-full max-w-5xl flex-1 flex-col gap-8 px-4 py-10 sm:px-6">
      <div className="flex flex-col gap-1">
        <Link href="/admin" className="text-sm text-indigo-700 hover:underline dark:text-indigo-300">
          ← Administración
        </Link>
        <h1 className="text-3xl font-semibold tracking-tight">Docentes</h1>
        <p className="text-zinc-600 dark:text-zinc-400">
          Cada profesor entra con su propia cuenta. Dale el rol de docente y asignale las materias que dicta: solo va a ver a los estudiantes de esas materias.
        </p>
      </div>

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

      <section aria-labelledby="agregar-titulo" className="flex flex-col gap-3">
        <h2 id="agregar-titulo" className="text-lg font-semibold">
          Agregar un docente
        </h2>
        <form onSubmit={buscar} aria-label="Buscar usuario" className="flex flex-wrap items-end gap-2">
          <label className="flex min-w-0 flex-1 flex-col gap-1 text-sm font-medium">
            Email, apellido o DNI
            <input
              value={texto}
              onChange={(e) => setTexto(e.target.value)}
              minLength={3}
              required
              className="rounded-md border border-zinc-300 bg-transparent px-3 py-2 dark:border-zinc-700 dark:bg-zinc-900"
            />
          </label>
          <button type="submit" className="rounded-md bg-indigo-600 px-4 py-2 font-medium text-white hover:bg-indigo-700">
            Buscar
          </button>
        </form>
        <p className="text-sm text-zinc-500">El profesor tiene que haber entrado a Astrea al menos una vez para aparecer.</p>
        {candidatos?.length === 0 && <p className="text-zinc-500">No se encontró ningún usuario.</p>}
        {candidatos && candidatos.length > 0 && (
          <ul aria-label="Usuarios encontrados" className="flex flex-col divide-y divide-zinc-200 rounded-xl border border-zinc-200 dark:divide-zinc-800 dark:border-zinc-800">
            {candidatos.map((usuario) => (
              <li key={usuario.id} className="flex flex-wrap items-center justify-between gap-3 px-4 py-3">
                <span>
                  <span className="font-medium">{usuario.nombre}</span>
                  {usuario.nombre !== usuario.email && <span className="block text-sm text-zinc-500">{usuario.email}</span>}
                </span>
                {usuario.esDocente ? (
                  <span className="text-sm text-zinc-500">Ya es docente</span>
                ) : (
                  <button
                    type="button"
                    className={botonChico}
                    aria-label={`Hacer docente a ${usuario.nombre}`}
                    onClick={() =>
                      void ejecutar(async () => {
                        await asistenciaApi.hacerDocente(await obtenerToken(), usuario.id);
                        setCandidatos(null);
                        setTexto("");
                      }, `${usuario.nombre} ahora es docente. Asignale sus materias.`)
                    }
                  >
                    Hacer docente
                  </button>
                )}
              </li>
            ))}
          </ul>
        )}
      </section>

      <section aria-labelledby="docentes-titulo" className="flex flex-col gap-4">
        <h2 id="docentes-titulo" className="text-lg font-semibold">
          Docentes y sus materias
        </h2>
        {!docentes ? (
          !error && (
            <p role="status" className="text-zinc-500">
              Cargando…
            </p>
          )
        ) : docentes.length === 0 ? (
          <p className="text-zinc-500">Todavía no hay docentes.</p>
        ) : (
          docentes.map((docente) => (
            <article key={docente.id} aria-label={`Docente ${docente.nombre}`} className="flex flex-col gap-3 rounded-xl border border-zinc-200 p-4 dark:border-zinc-800">
              <div className="flex flex-wrap items-start justify-between gap-2">
                <h3 className="font-semibold">
                  {docente.nombre}
                  {docente.nombre !== docente.email && <span className="block text-sm font-normal text-zinc-500">{docente.email}</span>}
                </h3>
                {quitando === docente.id ? (
                  <span className="flex flex-wrap items-center gap-2 text-sm">
                    ¿Quitarle el rol y sus materias?
                    <button
                      type="button"
                      className={botonChico}
                      onClick={() => void ejecutar(async () => asistenciaApi.quitarDocente(await obtenerToken(), docente.id), `${docente.nombre} ya no es docente.`)}
                    >
                      Sí, quitar
                    </button>
                    <button type="button" className={botonChico} onClick={() => setQuitando(null)}>
                      No
                    </button>
                  </span>
                ) : (
                  <button type="button" className={botonChico} onClick={() => setQuitando(docente.id)}>
                    Quitar rol de docente
                  </button>
                )}
              </div>
              {docente.materias.length === 0 ? (
                <p className="text-sm text-zinc-500">Sin materias asignadas.</p>
              ) : (
                <ul aria-label={`Materias de ${docente.nombre}`} className="flex flex-col gap-1">
                  {docente.materias.map((materia) => (
                    <li key={materia.id} className="flex flex-wrap items-center justify-between gap-2 text-sm">
                      <span>
                        {materia.materia} · <span className="text-zinc-500">{materia.carrera}, {NOMBRE_ANIO[materia.anio] ?? `año ${materia.anio}`}</span>
                      </span>
                      <button
                        type="button"
                        className={botonChico}
                        aria-label={`Quitar ${materia.materia} a ${docente.nombre}`}
                        onClick={() => void ejecutar(async () => asistenciaApi.quitarAsignacion(await obtenerToken(), materia.id), `Se quitó ${materia.materia}.`)}
                      >
                        Quitar
                      </button>
                    </li>
                  ))}
                </ul>
              )}
              <SelectorMateria
                titulo={`Asignar materia a ${docente.nombre}`}
                textoBoton="Asignar"
                onElegir={(planMateriaId) =>
                  ejecutar(async () => asistenciaApi.asignarMateria(await obtenerToken(), docente.id, planMateriaId), `Materia asignada a ${docente.nombre}.`)
                }
              />
            </article>
          ))
        )}
      </section>
    </main>
  );
}
