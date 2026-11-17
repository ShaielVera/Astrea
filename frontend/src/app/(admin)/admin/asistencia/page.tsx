"use client";

import Link from "next/link";
import { useCallback, useEffect, useState, type FormEvent } from "react";
import { useAuth } from "@/features/auth/AuthProvider";
import { mensajeDeError } from "@/features/auth/mensajes-error";
import { NOMBRE_ANIO, type Pagina } from "@/features/academico/api";
import { Paginacion } from "@/features/mesas/Paginacion";
import { asistenciaApi, ETIQUETA_VALOR, fechaCorta, type Avance, type Informe, type Relevamiento } from "@/features/asistencia/api";

/** Materias por página en el avance: así el informe queda a la vista sin bajar tanto. */
const MATERIAS_POR_PAGINA = 8;

function paginar<T>(items: T[], page: number, pageSize: number): Pagina<T> {
  const totalPages = Math.max(1, Math.ceil(items.length / pageSize));
  const actual = Math.min(Math.max(1, page), totalPages);
  return {
    items: items.slice((actual - 1) * pageSize, actual * pageSize),
    page: actual,
    pageSize,
    totalItems: items.length,
    totalPages,
    hasPrevious: actual > 1,
    hasNext: actual < totalPages,
  };
}

const campo = "rounded-md border border-zinc-300 bg-transparent px-3 py-2 dark:border-zinc-700 dark:bg-zinc-900";
const botonPrimario = "rounded-md bg-indigo-600 px-4 py-2 font-medium text-white hover:bg-indigo-700 disabled:opacity-60";
const botonChico =
  "rounded-md border border-zinc-300 px-3 py-1.5 text-sm font-medium hover:bg-zinc-100 disabled:opacity-60 dark:border-zinc-700 dark:hover:bg-zinc-800";

const ESTILO_VALOR = {
  AsisteRegularmente: "text-emerald-800 dark:text-emerald-300",
  AsisteIrregularmente: "text-amber-800 dark:text-amber-300",
  NoAsiste: "text-red-800 dark:text-red-300",
} as const;

/** Fecha de hoy + n días en formato yyyy-mm-dd (hora local). */
function enDias(n: number): string {
  const fecha = new Date();
  fecha.setDate(fecha.getDate() + n);
  return `${fecha.getFullYear()}-${String(fecha.getMonth() + 1).padStart(2, "0")}-${String(fecha.getDate()).padStart(2, "0")}`;
}

export default function AsistenciaAdmin() {
  const { obtenerToken } = useAuth();
  const [relevamientos, setRelevamientos] = useState<Relevamiento[] | null>(null);
  const [elegido, setElegido] = useState<string>("");
  const [titulo, setTitulo] = useState("");
  const [cierre, setCierre] = useState(() => enDias(14));
  const [error, setError] = useState<string | null>(null);
  const [aviso, setAviso] = useState<string | null>(null);

  const recargar = useCallback(async () => {
    const lista = await asistenciaApi.relevamientos(await obtenerToken());
    setRelevamientos(lista);
    return lista;
  }, [obtenerToken]);

  useEffect(() => {
    let vigente = true;
    obtenerToken()
      .then((token) => asistenciaApi.relevamientos(token))
      .then((lista) => {
        if (!vigente) return;
        setRelevamientos(lista);
        setElegido(lista[0]?.id ?? "");
      })
      .catch((e) => vigente && setError(mensajeDeError(e)));
    return () => {
      vigente = false;
    };
  }, [obtenerToken]);

  async function abrir(evento: FormEvent<HTMLFormElement>) {
    evento.preventDefault();
    setError(null);
    try {
      const nuevo = await asistenciaApi.abrirRelevamiento(await obtenerToken(), titulo.trim(), cierre);
      await recargar();
      setElegido(nuevo.id);
      setTitulo("");
      setAviso(`Se abrió "${nuevo.titulo}". Los profesores ya pueden confirmar hasta el ${fechaCorta(nuevo.fechaCierre)}.`);
    } catch (e) {
      setError(mensajeDeError(e));
    }
  }

  const relevamiento = relevamientos?.find((item) => item.id === elegido) ?? null;

  return (
    <main className="mx-auto flex w-full max-w-5xl flex-1 flex-col gap-8 px-4 py-10 sm:px-6">
      <div className="flex flex-col gap-1">
        <Link href="/admin" className="text-sm text-indigo-700 hover:underline dark:text-indigo-300">
          ← Administración
        </Link>
        <h1 className="text-3xl font-semibold tracking-tight">Confirmación de asistencia</h1>
        <p className="text-zinc-600 dark:text-zinc-400">
          Pedile a los profesores que confirmen quién asiste a sus materias (por ejemplo, antes del receso de julio para la beca Progresar). Astrea no guarda
          quién cobra una beca: el informe se filtra con la lista de DNI que tengas.
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

      <section aria-labelledby="abrir-titulo" className="flex flex-col gap-3 rounded-xl border border-zinc-200 bg-superficie p-5 shadow-sm dark:border-zinc-800">
        <h2 id="abrir-titulo" className="text-lg font-semibold">
          Pedir una confirmación
        </h2>
        <form onSubmit={abrir} aria-label="Abrir relevamiento" className="flex flex-wrap items-end gap-2">
          <label className="flex min-w-0 flex-1 flex-col gap-1 text-sm font-medium">
            Título
            <input value={titulo} onChange={(e) => setTitulo(e.target.value)} required maxLength={120} placeholder="Ej.: Antes del receso de julio" className={campo} />
          </label>
          <label className="flex flex-col gap-1 text-sm font-medium">
            Los profesores confirman hasta
            <input type="date" value={cierre} min={enDias(0)} onChange={(e) => setCierre(e.target.value)} required className={campo} />
          </label>
          <button type="submit" className={botonPrimario}>
            Abrir
          </button>
        </form>
      </section>

      {relevamientos && relevamientos.length > 0 && (
        <label className="flex max-w-xl flex-col gap-1 text-sm font-medium">
          Relevamiento
          <select value={elegido} onChange={(e) => setElegido(e.target.value)} className={campo}>
            {relevamientos.map((item) => (
              <option key={item.id} value={item.id}>
                {item.titulo} ({item.cicloLectivo}) — {item.abierto ? `abierto hasta el ${fechaCorta(item.fechaCierre)}` : "cerrado"}
              </option>
            ))}
          </select>
        </label>
      )}

      {relevamiento && (
        <DetalleRelevamiento
          key={relevamiento.id}
          relevamiento={relevamiento}
          onCerrado={async () => {
            await recargar();
            setAviso(`Se cerró "${relevamiento.titulo}".`);
          }}
        />
      )}
    </main>
  );
}

function DetalleRelevamiento({ relevamiento, onCerrado }: { relevamiento: Relevamiento; onCerrado: () => Promise<void> }) {
  const { obtenerToken } = useAuth();
  const [avance, setAvance] = useState<Avance | null>(null);
  const [paginaAvance, setPaginaAvance] = useState(1);
  const [informe, setInforme] = useState<Informe | null>(null);
  const [texto, setTexto] = useState("");
  const [dnis, setDnis] = useState("");
  const [confirmandoCierre, setConfirmandoCierre] = useState(false);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    let vigente = true;
    obtenerToken()
      .then((token) => asistenciaApi.avance(token, relevamiento.id))
      .then((resultado) => vigente && setAvance(resultado))
      .catch((e) => vigente && setError(mensajeDeError(e)));
    return () => {
      vigente = false;
    };
  }, [obtenerToken, relevamiento.id]);

  async function consultar(page = 1) {
    setError(null);
    try {
      setInforme(await asistenciaApi.informe(await obtenerToken(), relevamiento.id, { texto: texto.trim() || undefined, dnis: dnis.trim() || undefined, page }));
    } catch (e) {
      setError(mensajeDeError(e));
    }
  }

  async function cerrar() {
    try {
      await asistenciaApi.cerrarRelevamiento(await obtenerToken(), relevamiento.id);
      await onCerrado();
    } catch (e) {
      setError(mensajeDeError(e));
    }
  }

  function descargarCsv() {
    if (!informe) return;
    const celda = (valor: string | number | null) => `"${String(valor ?? "").replaceAll('"', '""')}"`;
    const filas = [["Estudiante", "Email", "DNI", "Materia", "Carrera", "Año", "Asistencia", "Observación", "Confirmó"].map(celda).join(";")];
    for (const estudiante of informe.estudiantes.items) {
      for (const materia of estudiante.materias) {
        filas.push(
          [estudiante.nombre, estudiante.email, estudiante.dni, materia.materia, materia.carrera, materia.anio, materia.valor ? ETIQUETA_VALOR[materia.valor] : "Sin confirmar", materia.observacion, materia.docente]
            .map(celda)
            .join(";"),
        );
      }
    }
    const url = URL.createObjectURL(new Blob(["﻿" + filas.join("\r\n")], { type: "text/csv;charset=utf-8" }));
    const enlace = document.createElement("a");
    enlace.href = url;
    enlace.download = `asistencia-${relevamiento.titulo.replace(/[^\p{L}\p{N}]+/gu, "-")}.csv`;
    enlace.click();
    URL.revokeObjectURL(url);
  }

  return (
    <div className="flex flex-col gap-8">
      {error && (
        <p role="alert" className="text-red-700 dark:text-red-300">
          {error}
        </p>
      )}

      <section aria-labelledby="avance-titulo" className="flex flex-col gap-3 rounded-xl border border-zinc-200 bg-superficie p-5 shadow-sm dark:border-zinc-800">
        <div className="flex flex-wrap items-center justify-between gap-2">
          <h2 id="avance-titulo" className="text-lg font-semibold">
            Avance de los profesores
          </h2>
          {relevamiento.abierto &&
            (confirmandoCierre ? (
              <span className="flex flex-wrap items-center gap-2 text-sm">
                ¿Cerrarlo ahora? Los profesores ya no van a poder cambiar nada.
                <button type="button" className={botonChico} onClick={() => void cerrar()}>
                  Sí, cerrar
                </button>
                <button type="button" className={botonChico} onClick={() => setConfirmandoCierre(false)}>
                  No
                </button>
              </span>
            ) : (
              <button type="button" className={botonChico} onClick={() => setConfirmandoCierre(true)}>
                Cerrar antes de tiempo
              </button>
            ))}
        </div>
        {avance && (
          <>
            <p data-testid="avance-total">
              {avance.confirmadas} de {avance.inscripciones} inscripciones confirmadas por los profesores.
            </p>
            {avance.materias.length > 0 && (
              <div className="overflow-x-auto">
                <table className="w-full text-left text-sm">
                  <caption className="sr-only">Avance por materia</caption>
                  <thead>
                    <tr className="border-b border-zinc-200 dark:border-zinc-800">
                      <th scope="col" className="py-2 pr-4">Materia</th>
                      <th scope="col" className="py-2 pr-4">Docentes</th>
                      <th scope="col" className="py-2 text-right">Confirmados</th>
                    </tr>
                  </thead>
                  <tbody>
                    {paginar(avance.materias, paginaAvance, MATERIAS_POR_PAGINA).items.map((materia) => (
                      <tr key={materia.planMateriaId} className="border-b border-zinc-100 dark:border-zinc-900">
                        <th scope="row" className="py-2 pr-4 font-normal">
                          {materia.materia}
                          <span className="block text-zinc-500">
                            {materia.carrera} · {NOMBRE_ANIO[materia.anio] ?? `Año ${materia.anio}`}
                          </span>
                        </th>
                        <td className="py-2 pr-4">{materia.docentes.length > 0 ? materia.docentes.join(" · ") : <span className="text-red-700 dark:text-red-300">Sin docente asignado</span>}</td>
                        <td className="py-2 text-right tabular-nums">
                          {materia.confirmados} / {materia.inscriptos}
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            )}
            <Paginacion pagina={paginar(avance.materias, paginaAvance, MATERIAS_POR_PAGINA)} onCambiar={setPaginaAvance} />
          </>
        )}
      </section>

      <section aria-labelledby="informe-titulo" className="flex flex-col gap-3 rounded-xl border border-zinc-200 bg-superficie p-5 shadow-sm dark:border-zinc-800">
        <h2 id="informe-titulo" className="text-lg font-semibold">
          Informe
        </h2>
        <form
          aria-label="Consultar informe"
          onSubmit={(evento) => {
            evento.preventDefault();
            void consultar();
          }}
          className="flex flex-col gap-3"
        >
          <label className="flex flex-col gap-1 text-sm font-medium">
            Lista de DNI (opcional)
            <textarea
              value={dnis}
              onChange={(e) => setDnis(e.target.value)}
              rows={4}
              placeholder={"30123456\n40.987.654"}
              aria-describedby="ayuda-dnis"
              className={campo}
            />
            <span id="ayuda-dnis" className="text-xs font-normal text-zinc-500">
              Pegá la lista completa de una vez (por ejemplo, la columna de DNI copiada de Excel o del listado del Progresar): uno por renglón o
              separados por coma. Los puntos se ignoran. Hasta 500 DNI.
            </span>
          </label>
          <label className="flex flex-col gap-1 text-sm font-medium">
            Buscar por nombre, email o DNI (opcional)
            <input value={texto} onChange={(e) => setTexto(e.target.value)} className={campo} />
          </label>
          <button type="submit" className={`${botonPrimario} self-start`}>
            Ver informe
          </button>
        </form>
        <p className="text-sm text-zinc-500">
          El informe muestra lo que confirmó cada profesor. Antes de decidir, revisá las faltas justificadas: la decisión es de la dirección.
        </p>

        {informe && (
          <>
            <div className="flex flex-wrap items-center justify-between gap-2">
              <p role="status">
                {informe.estudiantes.totalItems} {informe.estudiantes.totalItems === 1 ? "estudiante" : "estudiantes"}.
              </p>
              {informe.estudiantes.items.length > 0 && (
                <button type="button" className={botonChico} onClick={descargarCsv}>
                  Descargar esta página en CSV
                </button>
              )}
            </div>
            {informe.dnisSinDatos.length > 0 && (
              <p className="rounded-md bg-amber-50 px-4 py-2 text-sm text-amber-900 dark:bg-amber-950 dark:text-amber-200">
                Sin datos en Astrea ({informe.dnisSinDatos.length}): {informe.dnisSinDatos.join(", ")}. No están anotados en ninguna materia este año o Secretaría no
                cargó su DNI.
              </p>
            )}
            <ul aria-label="Estudiantes del informe" className="flex flex-col gap-3">
              {informe.estudiantes.items.map((estudiante) => (
                <li key={estudiante.id} aria-label={estudiante.nombre} className="rounded-xl border bg-superficie border-zinc-200 p-4 dark:border-zinc-800">
                  <div className="flex flex-wrap items-baseline justify-between gap-2">
                    <span className="font-medium">
                      {estudiante.nombre}
                      <span className="block text-sm font-normal text-zinc-500">
                        {estudiante.email}
                        {estudiante.dni ? ` · DNI ${estudiante.dni}` : ""}
                      </span>
                    </span>
                    <span className="text-sm">
                      {estudiante.regular} regular · {estudiante.irregular} irregular ·{" "}
                      <span className={estudiante.noAsiste > 0 ? "font-semibold text-red-800 dark:text-red-300" : ""}>{estudiante.noAsiste} no asiste</span> ·{" "}
                      {estudiante.sinConfirmar} sin confirmar
                    </span>
                  </div>
                  <ul className="mt-2 flex flex-col gap-1 text-sm">
                    {estudiante.materias.map((materia) => (
                      <li key={`${materia.carrera}-${materia.materia}`} className="flex flex-wrap justify-between gap-2">
                        <span>
                          {materia.materia} <span className="text-zinc-500">({materia.carrera})</span>
                        </span>
                        <span className={materia.valor ? ESTILO_VALOR[materia.valor] : "text-zinc-500"}>
                          {materia.valor ? ETIQUETA_VALOR[materia.valor] : "Sin confirmar"}
                          {materia.docente ? ` · ${materia.docente}` : ""}
                          {materia.observacion ? ` · “${materia.observacion}”` : ""}
                        </span>
                      </li>
                    ))}
                  </ul>
                </li>
              ))}
            </ul>
            {informe.estudiantes.totalPages > 1 && (
              <div className="flex items-center gap-3">
                <button type="button" className={botonChico} disabled={!informe.estudiantes.hasPrevious} onClick={() => void consultar(informe.estudiantes.page - 1)}>
                  Anterior
                </button>
                <span className="text-sm">
                  Página {informe.estudiantes.page} de {informe.estudiantes.totalPages}
                </span>
                <button type="button" className={botonChico} disabled={!informe.estudiantes.hasNext} onClick={() => void consultar(informe.estudiantes.page + 1)}>
                  Siguiente
                </button>
              </div>
            )}
          </>
        )}
      </section>
    </div>
  );
}
