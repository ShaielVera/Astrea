"use client";

import Link from "next/link";
import { useCallback, useEffect, useRef, useState, type FormEvent } from "react";
import { useAuth } from "@/features/auth/AuthProvider";
import { mensajeDeError } from "@/features/auth/mensajes-error";
import {
  HERRAMIENTAS,
  MOTIVOS,
  diaCorto,
  diaLargo,
  metricasApi,
  ultimosDias,
  type Cantidad,
  type ConsultasDelDia,
  type MetricasAsistente,
  type Tema,
} from "@/features/metricas/api";

const campo = "rounded-md border border-zinc-300 bg-transparent px-3 py-2 dark:border-zinc-700 dark:bg-zinc-900";
const tarjeta = "rounded-xl border border-zinc-200 p-4 dark:border-zinc-800";

// Paleta categórica validada (claro y oscuro) para las dos series del gráfico.
const COLOR_RESPONDIDAS = "bg-[#2a78d6] dark:bg-[#3987e5]";
const COLOR_SIN_RESPUESTA = "bg-[#eb6834] dark:bg-[#d95926]";

const PERIODOS = [7, 30, 90] as const;

type Periodo = { tipo: "ultimos"; dias: number } | { tipo: "rango"; desde: string; hasta: string };

export default function MetricasDelAsistente() {
  const { obtenerToken } = useAuth();
  const [periodo, setPeriodo] = useState<Periodo>({ tipo: "ultimos", dias: 30 });
  const [rango, setRango] = useState(() => ultimosDias(30));
  const [metricas, setMetricas] = useState<MetricasAsistente | null>(null);
  const [cargando, setCargando] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const ultimaCarga = useRef(0);

  const cargar = useCallback(
    async (elegido: Periodo) => {
      const numero = ++ultimaCarga.current;
      const { desde, hasta } = elegido.tipo === "ultimos" ? ultimosDias(elegido.dias) : elegido;
      setCargando(true);
      setError(null);
      try {
        const resultado = await metricasApi.asistente(await obtenerToken(), desde, hasta);
        if (numero === ultimaCarga.current) setMetricas(resultado);
      } catch (e) {
        if (numero === ultimaCarga.current) setError(mensajeDeError(e));
      } finally {
        if (numero === ultimaCarga.current) setCargando(false);
      }
    },
    [obtenerToken],
  );

  useEffect(() => {
    // Carga inicial: los últimos 30 días.
    const numero = ++ultimaCarga.current;
    const { desde, hasta } = ultimosDias(30);
    obtenerToken()
      .then((token) => metricasApi.asistente(token, desde, hasta))
      .then((resultado) => numero === ultimaCarga.current && setMetricas(resultado))
      .catch((e) => numero === ultimaCarga.current && setError(mensajeDeError(e)))
      .finally(() => numero === ultimaCarga.current && setCargando(false));
  }, [obtenerToken]);

  function elegir(nuevo: Periodo) {
    setPeriodo(nuevo);
    void cargar(nuevo);
  }

  function aplicarRango(evento: FormEvent<HTMLFormElement>) {
    evento.preventDefault();
    elegir({ tipo: "rango", ...rango });
  }

  return (
    <main className="mx-auto flex w-full max-w-5xl flex-1 flex-col gap-8 px-4 py-10 sm:px-6">
      <div className="flex flex-col gap-1">
        <Link href="/admin" className="text-sm text-indigo-700 hover:underline dark:text-indigo-300">
          ← Administración
        </Link>
        <h1 className="text-3xl font-semibold tracking-tight">Métricas del asistente</h1>
        <p className="text-zinc-600 dark:text-zinc-400">
          Cuánto se usa, qué se pregunta y qué temas quedan sin respuesta. Los datos son anónimos: no muestran quién preguntó.
        </p>
      </div>

      <section aria-label="Período" className="flex flex-wrap items-end gap-3">
        <div role="group" aria-label="Últimos días" className="flex gap-2">
          {PERIODOS.map((dias) => {
            const activo = periodo.tipo === "ultimos" && periodo.dias === dias;
            return (
              <button
                key={dias}
                type="button"
                aria-pressed={activo}
                onClick={() => elegir({ tipo: "ultimos", dias })}
                className={`rounded-md border px-3 py-2 text-sm font-medium ${
                  activo
                    ? "border-indigo-600 bg-indigo-600 text-white"
                    : "border-zinc-300 hover:bg-zinc-100 dark:border-zinc-700 dark:hover:bg-zinc-800"
                }`}
              >
                Últimos {dias} días
              </button>
            );
          })}
        </div>
        <form onSubmit={aplicarRango} aria-label="Elegir fechas" className="flex flex-wrap items-end gap-2">
          <label className="flex flex-col gap-1 text-sm font-medium">
            Desde
            <input type="date" required value={rango.desde} max={rango.hasta} onChange={(e) => setRango({ ...rango, desde: e.target.value })} className={campo} />
          </label>
          <label className="flex flex-col gap-1 text-sm font-medium">
            Hasta
            <input type="date" required value={rango.hasta} min={rango.desde} onChange={(e) => setRango({ ...rango, hasta: e.target.value })} className={campo} />
          </label>
          <button type="submit" className="rounded-md border border-zinc-300 px-3 py-2 text-sm font-medium hover:bg-zinc-100 dark:border-zinc-700 dark:hover:bg-zinc-800">
            Ver período
          </button>
        </form>
      </section>

      {error && (
        <p role="alert" className="text-red-700 dark:text-red-300">
          {error}
        </p>
      )}
      {cargando && !metricas && (
        <p role="status" className="text-zinc-500">
          Cargando…
        </p>
      )}

      {metricas && (
        <div className={`flex flex-col gap-8 ${cargando ? "opacity-60" : ""}`} aria-busy={cargando}>
          <p className="text-sm text-zinc-600 dark:text-zinc-400">
            Del {diaLargo(metricas.desde)} al {diaLargo(metricas.hasta)}.
          </p>
          <Resumen metricas={metricas} />
          <GraficoPorDia dias={metricas.porDia} />
          <div className="grid gap-6 md:grid-cols-2">
            <ListaDeTemas
              id="sin-respuesta"
              titulo="Temas sin respuesta"
              descripcion="Lo que el asistente no supo responder: puede faltar cargar un documento."
              vacio="Ninguna pregunta quedó sin respuesta en este período."
              temas={metricas.sinRespuesta}
            />
            <ListaDeTemas
              id="frecuentes"
              titulo="Preguntas frecuentes"
              descripcion="Temas que se repitieron al menos 2 veces."
              vacio="Todavía no hay preguntas repetidas."
              temas={metricas.frecuentes}
            />
            <ListaDeCantidades
              id="herramientas"
              titulo="Datos de Astrea consultados"
              vacio="El asistente no consultó datos de Astrea en este período."
              filas={metricas.herramientas}
              etiquetas={HERRAMIENTAS}
            />
            <ListaDeCantidades
              id="motivos"
              titulo="Por qué no respondió"
              vacio="No hubo fallas en este período."
              filas={metricas.motivos}
              etiquetas={MOTIVOS}
            />
          </div>
        </div>
      )}
    </main>
  );
}

function Resumen({ metricas }: { metricas: MetricasAsistente }) {
  const { resumen } = metricas;
  const tiles: { etiqueta: string; valor: string; detalle?: string }[] = [
    { etiqueta: "Consultas", valor: String(resumen.total) },
    {
      etiqueta: "Respondidas con fuentes",
      valor: resumen.porcentajeRespondidas === null ? "—" : `${resumen.porcentajeRespondidas}%`,
      detalle: `${resumen.respondidas} de ${resumen.total}`,
    },
    { etiqueta: "Sin respuesta", valor: String(resumen.sinEvidencia), detalle: "derivadas a Secretaría" },
    { etiqueta: "Errores de la IA", valor: String(resumen.errores), detalle: "el proveedor no respondió" },
    { etiqueta: "Pedidos de dato faltante", valor: String(resumen.aclaraciones) },
    { etiqueta: "Trámites propuestos", valor: String(resumen.propuestasDeTramite) },
    {
      etiqueta: "Tiempo promedio de respuesta",
      valor: resumen.total === 0 ? "—" : `${(resumen.duracionPromedioMs / 1000).toLocaleString("es-AR", { maximumFractionDigits: 1 })} s`,
    },
  ];

  return (
    <section aria-labelledby="resumen-titulo" className="flex flex-col gap-3">
      <h2 id="resumen-titulo" className="text-lg font-semibold">
        Resumen
      </h2>
      <dl className="grid grid-cols-2 gap-3 sm:grid-cols-3 lg:grid-cols-4">
        {tiles.map((tile) => (
          <div key={tile.etiqueta} className={tarjeta}>
            <dt className="text-sm text-zinc-600 dark:text-zinc-400">{tile.etiqueta}</dt>
            <dd className="text-3xl font-semibold tabular-nums">{tile.valor}</dd>
            {tile.detalle && <dd className="text-sm text-zinc-500">{tile.detalle}</dd>}
          </div>
        ))}
      </dl>
    </section>
  );
}

/** Barras apiladas por día: respondidas (abajo) y sin respuesta (arriba), con tooltip al pasar y vista de tabla. */
function GraficoPorDia({ dias }: { dias: ConsultasDelDia[] }) {
  const [activo, setActivo] = useState<number | null>(null);
  const maximo = Math.max(1, ...dias.map((dia) => dia.total));
  const total = dias.reduce((suma, dia) => suma + dia.total, 0);
  const sinRespuesta = dias.reduce((suma, dia) => suma + dia.sinRespuesta, 0);
  const separacion = dias.length > 60 ? "gap-px" : "gap-0.5";
  const seleccionado = activo === null ? null : dias[activo];
  const etiquetas = new Set([0, Math.floor((dias.length - 1) / 2), dias.length - 1]);

  return (
    <section aria-labelledby="por-dia-titulo" className="flex flex-col gap-3">
      <div className="flex flex-wrap items-baseline justify-between gap-2">
        <h2 id="por-dia-titulo" className="text-lg font-semibold">
          Consultas por día
        </h2>
        <ul aria-label="Referencias" className="flex gap-4 text-sm text-zinc-700 dark:text-zinc-300">
          <li className="flex items-center gap-2">
            <span aria-hidden className={`inline-block size-3 rounded-sm ${COLOR_RESPONDIDAS}`} />
            Con respuesta
          </li>
          <li className="flex items-center gap-2">
            <span aria-hidden className={`inline-block size-3 rounded-sm ${COLOR_SIN_RESPUESTA}`} />
            Sin respuesta o con error
          </li>
        </ul>
      </div>

      <div className={`${tarjeta} relative`}>
        <p className="mb-2 h-5 text-sm text-zinc-600 dark:text-zinc-400" aria-live="polite">
          {seleccionado
            ? `${diaLargo(seleccionado.dia)}: ${consultas(seleccionado.total)}, ${seleccionado.sinRespuesta} sin respuesta`
            : total === 0
              ? "No hubo consultas en el período."
              : `Máximo diario: ${consultas(maximo)}. Pasá el mouse sobre una barra para ver el día.`}
        </p>
        <div
          role="img"
          aria-label={`Gráfico de consultas por día: ${total} en total, ${sinRespuesta} sin respuesta. Los valores de cada día están en la tabla.`}
          className={`flex h-40 items-end border-b border-zinc-300 dark:border-zinc-700 ${separacion}`}
          onMouseLeave={() => setActivo(null)}
        >
          {dias.map((dia, i) => {
            const respondidas = dia.total - dia.sinRespuesta;
            return (
              <div
                key={dia.dia}
                className={`flex h-full min-w-0 flex-1 flex-col justify-end ${activo === i ? "bg-zinc-100 dark:bg-zinc-800/60" : ""}`}
                onMouseEnter={() => setActivo(i)}
              >
                {dia.sinRespuesta > 0 && (
                  <div
                    className={`${COLOR_SIN_RESPUESTA} ${respondidas > 0 ? "mb-0.5" : ""} rounded-t-[4px]`}
                    style={{ height: `${(dia.sinRespuesta / maximo) * 100}%` }}
                  />
                )}
                {respondidas > 0 && (
                  <div
                    className={`${COLOR_RESPONDIDAS} ${dia.sinRespuesta > 0 ? "" : "rounded-t-[4px]"}`}
                    style={{ height: `${(respondidas / maximo) * 100}%` }}
                  />
                )}
              </div>
            );
          })}
        </div>
        <div aria-hidden className="mt-1 flex text-xs text-zinc-500">
          {dias.map((dia, i) => (
            <span key={dia.dia} className="min-w-0 flex-1 overflow-visible whitespace-nowrap">
              {etiquetas.has(i) ? diaCorto(dia.dia) : ""}
            </span>
          ))}
        </div>
      </div>

      <details className="text-sm">
        <summary className="cursor-pointer font-medium text-indigo-700 dark:text-indigo-300">Ver como tabla</summary>
        <div className="mt-2 max-h-72 overflow-auto">
          <table className="w-full text-left">
            <caption className="sr-only">Consultas por día</caption>
            <thead>
              <tr className="border-b border-zinc-200 dark:border-zinc-800">
                <th scope="col" className="py-1 pr-4">
                  Día
                </th>
                <th scope="col" className="py-1 pr-4 text-right">
                  Consultas
                </th>
                <th scope="col" className="py-1 text-right">
                  Sin respuesta
                </th>
              </tr>
            </thead>
            <tbody>
              {dias.map((dia) => (
                <tr key={dia.dia} className="border-b border-zinc-100 dark:border-zinc-900">
                  <th scope="row" className="py-1 pr-4 font-normal">
                    {diaLargo(dia.dia)}
                  </th>
                  <td className="py-1 pr-4 text-right tabular-nums">{dia.total}</td>
                  <td className="py-1 text-right tabular-nums">{dia.sinRespuesta}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      </details>
    </section>
  );
}

function ListaDeTemas({ id, titulo, descripcion, vacio, temas }: { id: string; titulo: string; descripcion: string; vacio: string; temas: Tema[] }) {
  return (
    <section aria-labelledby={`${id}-titulo`} className={`${tarjeta} flex flex-col gap-2`}>
      <h2 id={`${id}-titulo`} className="text-lg font-semibold">
        {titulo}
      </h2>
      <p className="text-sm text-zinc-600 dark:text-zinc-400">{descripcion}</p>
      {temas.length === 0 ? (
        <p className="text-sm text-zinc-500">{vacio}</p>
      ) : (
        <ol className="flex flex-col divide-y divide-zinc-100 dark:divide-zinc-900">
          {temas.map((tema) => (
            <li key={tema.tema} className="flex items-baseline justify-between gap-3 py-2">
              <span className="min-w-0 break-words">{tema.tema}</span>
              <span className="shrink-0 text-sm tabular-nums text-zinc-600 dark:text-zinc-400">
                {tema.cantidad} {tema.cantidad === 1 ? "vez" : "veces"}
              </span>
            </li>
          ))}
        </ol>
      )}
    </section>
  );
}

function ListaDeCantidades({ id, titulo, vacio, filas, etiquetas }: { id: string; titulo: string; vacio: string; filas: Cantidad[]; etiquetas: Record<string, string> }) {
  const maximo = Math.max(1, ...filas.map((fila) => fila.cantidad));
  return (
    <section aria-labelledby={`${id}-titulo`} className={`${tarjeta} flex flex-col gap-2`}>
      <h2 id={`${id}-titulo`} className="text-lg font-semibold">
        {titulo}
      </h2>
      {filas.length === 0 ? (
        <p className="text-sm text-zinc-500">{vacio}</p>
      ) : (
        <ul className="flex flex-col gap-3">
          {filas.map((fila) => (
            <li key={fila.nombre} className="flex flex-col gap-1">
              <div className="flex items-baseline justify-between gap-3 text-sm">
                <span>{etiquetas[fila.nombre] ?? fila.nombre}</span>
                <span className="tabular-nums text-zinc-600 dark:text-zinc-400">{fila.cantidad}</span>
              </div>
              <div aria-hidden className="h-1.5 rounded-full bg-zinc-100 dark:bg-zinc-800">
                <div className={`h-full rounded-full ${COLOR_RESPONDIDAS}`} style={{ width: `${(fila.cantidad / maximo) * 100}%` }} />
              </div>
            </li>
          ))}
        </ul>
      )}
    </section>
  );
}

function consultas(cantidad: number): string {
  return `${cantidad} ${cantidad === 1 ? "consulta" : "consultas"}`;
}
