"use client";

import Link from "next/link";
import { useEffect, useState } from "react";
import { useAuth } from "@/features/auth/AuthProvider";
import { mensajeDeError } from "@/features/auth/mensajes-error";
import { ROLES } from "@/features/auth/tipos";
import { NOMBRE_ANIO, type Pagina } from "@/features/academico/api";
import { SelectorMateria } from "@/features/asistencia/SelectorMateria";
import { asistenciaApi } from "@/features/asistencia/api";
import { haceCuanto, usuariosApi, type Cuenta, type RolEditable } from "@/features/usuarios/api";

const botonChico =
  "rounded-md border border-zinc-300 px-3 py-1.5 text-sm font-medium hover:bg-zinc-100 disabled:opacity-60 dark:border-zinc-700 dark:hover:bg-zinc-800";

const NOMBRE_ROL: Record<RolEditable, string> = { ADMINISTRADOR: "Administrador", DOCENTE: "Docente" };

type Pendiente = { cuenta: Cuenta; rol: RolEditable };

export default function UsuariosYRoles() {
  const { obtenerToken, perfil } = useAuth();
  const [texto, setTexto] = useState("");
  const [buscado, setBuscado] = useState("");
  const [pagina, setPagina] = useState(1);
  const [cuentas, setCuentas] = useState<Pagina<Cuenta> | null>(null);
  const [recarga, setRecarga] = useState(0);
  const [aviso, setAviso] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [aQuitar, setAQuitar] = useState<Pendiente | null>(null);
  const [abierta, setAbierta] = useState<string | null>(null);
  const [trabajando, setTrabajando] = useState(false);
  // La casilla cambia en el momento; cuando llega la lista actualizada (o un error) vuelve a mostrar lo guardado.
  const [marcados, setMarcados] = useState<Record<string, boolean>>({});

  // Busca mientras se escribe (con una pausa corta para no consultar en cada tecla).
  useEffect(() => {
    const espera = setTimeout(() => {
      setBuscado(texto);
      setPagina(1);
    }, 300);
    return () => clearTimeout(espera);
  }, [texto]);

  useEffect(() => {
    let vigente = true;
    obtenerToken()
      .then((token) => usuariosApi.listar(token, buscado, pagina))
      .then((resultado) => {
        if (vigente) {
          setCuentas(resultado);
          setMarcados({});
          setError(null);
        }
      })
      .catch((e) => vigente && setError(mensajeDeError(e)));
    return () => {
      vigente = false;
    };
  }, [obtenerToken, buscado, pagina, recarga]);

  async function ejecutar(accion: () => Promise<unknown>, mensaje: string) {
    setTrabajando(true);
    setError(null);
    setAviso(null);
    try {
      await accion();
      setAviso(mensaje);
      setAQuitar(null);
      setRecarga((n) => n + 1);
    } catch (e) {
      setError(mensajeDeError(e));
      setMarcados({});
    } finally {
      setTrabajando(false);
    }
  }

  function cambiarRol(cuenta: Cuenta, rol: RolEditable, dar: boolean) {
    setMarcados((actuales) => ({ ...actuales, [`${cuenta.id}:${rol}`]: dar }));
    if (!dar) {
      setAQuitar({ cuenta, rol });
      return;
    }
    void ejecutar(
      async () => usuariosApi.darRol(await obtenerToken(), cuenta.id, rol),
      rol === "DOCENTE" ? `${cuenta.nombre} ahora es docente. Asignale las materias que dicta.` : `${cuenta.nombre} ahora es administrador.`,
    );
    if (rol === "DOCENTE") setAbierta(cuenta.id);
  }

  return (
    <main className="mx-auto flex w-full max-w-5xl flex-1 flex-col gap-6 px-4 py-10 sm:px-6">
      <div className="flex flex-col gap-1">
        <Link href="/admin" className="text-sm text-indigo-700 hover:underline dark:text-indigo-300">
          ← Administración
        </Link>
        <h1 className="text-3xl font-semibold tracking-tight">Usuarios y roles</h1>
        <p className="text-zinc-600 dark:text-zinc-400">
          Todas las cuentas que entraron a Astrea, las más recientes arriba. Toda cuenta es estudiante; desde acá le das o le quitás los roles de administrador y
          docente, y a los docentes les asignás las materias que dictan.
        </p>
      </div>

      <label className="flex flex-col gap-1 text-sm font-medium">
        Buscar por apellido, nombre o DNI
        <input
          type="search"
          value={texto}
          onChange={(e) => setTexto(e.target.value)}
          placeholder="Ej.: Vera Brenda o 40123456"
          maxLength={100}
          className="rounded-md border border-zinc-300 bg-superficie px-3 py-2 dark:border-zinc-700"
        />
      </label>

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

      {!cuentas ? (
        !error && (
          <p role="status" className="text-zinc-500">
            Cargando…
          </p>
        )
      ) : cuentas.items.length === 0 ? (
        <p className="text-zinc-500">{buscado.trim() ? "No se encontró ninguna cuenta con esa búsqueda." : "Todavía no hay cuentas."}</p>
      ) : (
        <>
          <p className="text-sm text-zinc-500">
            {cuentas.totalItems} {cuentas.totalItems === 1 ? "cuenta" : "cuentas"}
            {buscado.trim() ? " encontradas" : ""}.
          </p>
          <ul aria-label="Cuentas" className="flex flex-col gap-3">
            {cuentas.items.map((cuenta) => {
              const esAdmin = cuenta.roles.includes(ROLES.administrador);
              const esDocente = cuenta.roles.includes(ROLES.docente);
              const soyYo = cuenta.id === perfil?.id;
              return (
                <li key={cuenta.id} aria-label={cuenta.nombre} className="flex flex-col gap-3 rounded-xl border border-zinc-200 bg-superficie p-4 dark:border-zinc-800">
                  <div className="flex flex-wrap items-start justify-between gap-3">
                    <div className="min-w-0">
                      <p className="font-semibold">
                        {cuenta.nombre}
                        {soyYo && <span className="ml-2 text-sm font-normal text-zinc-500">(vos)</span>}
                        {!cuenta.activa && <span className="ml-2 rounded-full bg-zinc-200 px-2 py-0.5 text-xs dark:bg-zinc-800">Desactivada</span>}
                      </p>
                      <p className="break-all text-sm text-zinc-500">
                        {cuenta.nombre !== cuenta.email && <>{cuenta.email} · </>}
                        {cuenta.dni ? `DNI ${cuenta.dni}` : "Sin DNI cargado"}
                      </p>
                      <p className="text-sm text-zinc-500">Último ingreso: {haceCuanto(cuenta.ultimoIngresoEn)}</p>
                    </div>
                    <fieldset className="flex flex-wrap gap-4" disabled={trabajando || !cuenta.activa}>
                      <legend className="sr-only">Roles de {cuenta.nombre}</legend>
                      {(["ADMINISTRADOR", "DOCENTE"] as const).map((rol) => {
                        const guardado = rol === "ADMINISTRADOR" ? esAdmin : esDocente;
                        const tiene = marcados[`${cuenta.id}:${rol}`] ?? guardado;
                        const bloqueado = rol === "ADMINISTRADOR" && soyYo && guardado;
                        return (
                          <label
                            key={rol}
                            className="flex items-center gap-2 text-sm font-medium"
                            title={bloqueado ? "No podés quitarte a vos mismo el rol de administrador." : undefined}
                          >
                            <input
                              type="checkbox"
                              className="size-4 accent-indigo-700"
                              checked={tiene}
                              disabled={bloqueado}
                              onChange={(e) => cambiarRol(cuenta, rol, e.target.checked)}
                            />
                            {NOMBRE_ROL[rol]}
                          </label>
                        );
                      })}
                    </fieldset>
                  </div>

                  {aQuitar?.cuenta.id === cuenta.id && (
                    <div role="alertdialog" aria-label="Confirmar" className="flex flex-wrap items-center gap-2 rounded-md bg-amber-50 px-3 py-2 text-sm text-amber-950 dark:bg-amber-950/50 dark:text-amber-100">
                      {aQuitar.rol === "DOCENTE"
                        ? `¿Quitarle el rol de docente a ${cuenta.nombre}? También se le quitan sus materias (las confirmaciones que ya hizo se conservan).`
                        : `¿Quitarle el rol de administrador a ${cuenta.nombre}? Deja de ver el panel de Administración.`}
                      <button
                        type="button"
                        className={botonChico}
                        disabled={trabajando}
                        onClick={() =>
                          void ejecutar(
                            async () => usuariosApi.quitarRol(await obtenerToken(), cuenta.id, aQuitar.rol),
                            `${cuenta.nombre} ya no es ${aQuitar.rol === "DOCENTE" ? "docente" : "administrador"}.`,
                          )
                        }
                      >
                        Sí, quitar
                      </button>
                      <button
                        type="button"
                        className={botonChico}
                        onClick={() => {
                          setAQuitar(null);
                          setMarcados({});
                        }}
                      >
                        No
                      </button>
                    </div>
                  )}

                  {esDocente && (
                    <div className="flex flex-col gap-2 border-t border-zinc-200 pt-3 dark:border-zinc-800">
                      <button
                        type="button"
                        aria-expanded={abierta === cuenta.id}
                        onClick={() => setAbierta(abierta === cuenta.id ? null : cuenta.id)}
                        className="self-start text-sm font-semibold text-indigo-800 hover:underline dark:text-indigo-200"
                      >
                        {abierta === cuenta.id ? "▾" : "▸"} Materias que dicta ({cuenta.materias.length})
                      </button>
                      {abierta === cuenta.id && (
                        <>
                          {cuenta.materias.length === 0 ? (
                            <p className="text-sm text-zinc-500">Sin materias asignadas.</p>
                          ) : (
                            <ul aria-label={`Materias de ${cuenta.nombre}`} className="flex flex-col gap-1">
                              {cuenta.materias.map((materia) => (
                                <li key={materia.id} className="flex flex-wrap items-center justify-between gap-2 text-sm">
                                  <span>
                                    {materia.materia} ·{" "}
                                    <span className="text-zinc-500">
                                      {materia.carrera}, {NOMBRE_ANIO[materia.anio] ?? `año ${materia.anio}`}
                                    </span>
                                  </span>
                                  <button
                                    type="button"
                                    className={botonChico}
                                    disabled={trabajando}
                                    aria-label={`Quitar ${materia.materia} a ${cuenta.nombre}`}
                                    onClick={() =>
                                      void ejecutar(async () => asistenciaApi.quitarAsignacion(await obtenerToken(), materia.id), `Se quitó ${materia.materia}.`)
                                    }
                                  >
                                    Quitar
                                  </button>
                                </li>
                              ))}
                            </ul>
                          )}
                          <SelectorMateria
                            titulo={`Asignar materia a ${cuenta.nombre}`}
                            textoBoton="Asignar"
                            yaElegidas={cuenta.materias.map((materia) => materia.planMateriaId)}
                            textoYaElegida="Ya asignada"
                            onElegir={(planMateriaId) =>
                              ejecutar(
                                async () => asistenciaApi.asignarMateria(await obtenerToken(), cuenta.id, planMateriaId),
                                `Materia asignada a ${cuenta.nombre}.`,
                              )
                            }
                          />
                        </>
                      )}
                    </div>
                  )}
                </li>
              );
            })}
          </ul>

          {cuentas.totalPages > 1 && (
            <nav aria-label="Páginas" className="flex items-center justify-between gap-3">
              <button type="button" className={botonChico} disabled={pagina <= 1} onClick={() => setPagina((p) => p - 1)}>
                ← Más recientes
              </button>
              <span className="text-sm text-zinc-500">
                Página {pagina} de {cuentas.totalPages}
              </span>
              <button type="button" className={botonChico} disabled={pagina >= cuentas.totalPages} onClick={() => setPagina((p) => p + 1)}>
                Más antiguas →
              </button>
            </nav>
          )}
        </>
      )}
    </main>
  );
}
