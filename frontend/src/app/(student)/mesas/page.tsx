"use client";

import { useCallback, useEffect, useRef, useState, type FormEvent } from "react";
import { useAuth } from "@/features/auth/AuthProvider";
import { mensajeDeError } from "@/features/auth/mensajes-error";
import { academicoApi, type Carrera, type MateriaDePlan, type Pagina } from "@/features/academico/api";
import { hoyEnElInstituto, mesasApi, type FiltroMesas, type MesaExamen } from "@/features/mesas/api";
import { ListadoMesas } from "@/features/mesas/ListadoMesas";
import { Paginacion } from "@/features/mesas/Paginacion";

const campo = "w-full min-w-0 rounded-md border border-zinc-300 bg-transparent px-3 py-2 font-normal dark:border-zinc-700 dark:bg-zinc-900";
const botonPrimario = "rounded-md bg-indigo-600 px-4 py-2 font-medium text-white hover:bg-indigo-700 disabled:opacity-60";
const POR_PAGINA = 10;

export default function MesasPage() {
  const { obtenerToken } = useAuth();
  const [carreras, setCarreras] = useState<Carrera[] | null>(null);
  const [materias, setMaterias] = useState<MateriaDePlan[]>([]);
  const [filtro, setFiltro] = useState<FiltroMesas>({ desde: hoyEnElInstituto() });
  const [aplicado, setAplicado] = useState<FiltroMesas>({ desde: hoyEnElInstituto() });
  const [pagina, setPagina] = useState<Pagina<MesaExamen> | null>(null);
  const [cargando, setCargando] = useState(false);
  const [error, setError] = useState<string | null>(null);
  // Numera las búsquedas: si llega la respuesta de una búsqueda vieja, no pisa a la más reciente.
  const ultimaBusqueda = useRef(0);

  const buscar = useCallback(
    async (criterio: FiltroMesas, page: number) => {
      const numero = ++ultimaBusqueda.current;
      setCargando(true);
      setError(null);
      try {
        const resultado = await mesasApi.buscar(await obtenerToken(), { ...criterio, page, pageSize: POR_PAGINA });
        if (numero !== ultimaBusqueda.current) return;
        setPagina(resultado);
        setAplicado(criterio);
      } catch (e) {
        if (numero === ultimaBusqueda.current) setError(mensajeDeError(e));
      } finally {
        if (numero === ultimaBusqueda.current) setCargando(false);
      }
    },
    [obtenerToken],
  );

  // Carga inicial: carreras para los filtros y las mesas desde hoy.
  useEffect(() => {
    let vigente = true;
    const numero = ++ultimaBusqueda.current;
    obtenerToken()
      .then((token) =>
        Promise.all([academicoApi.carreras(token), mesasApi.buscar(token, { desde: hoyEnElInstituto(), page: 1, pageSize: POR_PAGINA })]),
      )
      .then(([lista, primeras]) => {
        if (!vigente) return;
        setCarreras(lista);
        if (numero === ultimaBusqueda.current) setPagina(primeras);
      })
      .catch((e) => vigente && setError(mensajeDeError(e)));
    return () => {
      vigente = false;
    };
  }, [obtenerToken]);

  async function elegirCarrera(carreraId: string) {
    setFiltro((actual) => ({ ...actual, carreraId: carreraId || undefined, materiaId: undefined }));
    setMaterias([]);
    if (!carreraId) return;
    try {
      const token = await obtenerToken();
      const [plan] = await academicoApi.planes(token, carreraId);
      if (plan) setMaterias(await academicoApi.materias(token, plan.id));
    } catch (e) {
      setError(mensajeDeError(e));
    }
  }

  function enviar(evento: FormEvent<HTMLFormElement>) {
    evento.preventDefault();
    void buscar(filtro, 1);
  }

  const actualizar = (cambios: Partial<FiltroMesas>) => setFiltro((actual) => ({ ...actual, ...cambios }));

  return (
    <main className="mx-auto flex w-full max-w-5xl flex-1 flex-col gap-6 px-4 py-10 sm:px-6">
      <div className="flex flex-col gap-1">
        <h1 className="text-3xl font-semibold tracking-tight">Mesas de examen</h1>
        <p className="text-zinc-600 dark:text-zinc-400">Llamados a examen final con fecha, hora, aula y los profesores que toman el examen. Los horarios son de Argentina.</p>
      </div>

      <form onSubmit={enviar} aria-label="Buscar mesas" className="grid grid-cols-1 gap-3 rounded-xl border bg-superficie border-zinc-200 p-4 sm:grid-cols-2 lg:grid-cols-4 dark:border-zinc-800">
        <label className="flex min-w-0 flex-col gap-1 text-sm font-medium sm:col-span-2">
          Carrera
          <select value={filtro.carreraId ?? ""} onChange={(e) => void elegirCarrera(e.target.value)} className={campo}>
            <option value="">{carreras ? "Todas las carreras" : "Cargando carreras…"}</option>
            {carreras?.map((c) => (
              <option key={c.id} value={c.id}>
                {c.nombre}
              </option>
            ))}
          </select>
        </label>
        <label className="flex min-w-0 flex-col gap-1 text-sm font-medium sm:col-span-2">
          Materia
          <select
            value={filtro.materiaId ?? ""}
            onChange={(e) => actualizar({ materiaId: e.target.value || undefined })}
            disabled={materias.length === 0}
            className={campo}
          >
            <option value="">{filtro.carreraId ? "Todas las materias" : "Elegí primero una carrera"}</option>
            {materias.map((m) => (
              <option key={m.materiaId} value={m.materiaId}>
                {m.anio}.º · {m.nombre}
              </option>
            ))}
          </select>
        </label>
        <label className="flex min-w-0 flex-col gap-1 text-sm font-medium">
          Desde
          <input type="date" value={filtro.desde ?? ""} onChange={(e) => actualizar({ desde: e.target.value || undefined })} className={campo} />
        </label>
        <label className="flex min-w-0 flex-col gap-1 text-sm font-medium">
          Hasta
          <input type="date" value={filtro.hasta ?? ""} onChange={(e) => actualizar({ hasta: e.target.value || undefined })} className={campo} />
        </label>
        <label className="flex min-w-0 flex-col gap-1 text-sm font-medium">
          Materia o docente
          <input
            type="search"
            value={filtro.texto ?? ""}
            onChange={(e) => actualizar({ texto: e.target.value || undefined })}
            maxLength={100}
            placeholder="Ej.: programación, Gómez"
            className={campo}
          />
        </label>
        <div className="flex items-end">
          <button type="submit" className={`${botonPrimario} w-full`} disabled={cargando}>
            Buscar
          </button>
        </div>
      </form>

      {error && (
        <p role="alert" className="rounded-md bg-red-50 px-3 py-2 text-sm text-red-700 dark:bg-red-950 dark:text-red-300">
          {error}
        </p>
      )}

      <section aria-label="Resultados" aria-busy={cargando} className="flex flex-col gap-4">
        {pagina && (
          <p role="status" className="text-sm text-zinc-500">
            {pagina.totalItems === 0
              ? "No hay mesas publicadas con esos filtros."
              : `${pagina.totalItems} ${pagina.totalItems === 1 ? "mesa encontrada" : "mesas encontradas"}.`}
          </p>
        )}
        {pagina && <ListadoMesas mesas={pagina.items} />}
        {pagina && <Paginacion pagina={pagina} onCambiar={(page) => void buscar(aplicado, page)} />}
      </section>
    </main>
  );
}
