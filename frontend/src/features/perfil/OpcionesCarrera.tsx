"use client";

import { useEffect, useState } from "react";
import { useAuth } from "@/features/auth/AuthProvider";
import type { Carrera } from "@/features/academico/api";
import { perfilApi } from "./api";

/** Las carreras que el usuario cargó en "Mi perfil": null mientras carga, vacío si no cargó ninguna. */
export function useMisCarreras(): string[] | null {
  const { obtenerToken } = useAuth();
  const [mias, setMias] = useState<string[] | null>(null);

  useEffect(() => {
    let vigente = true;
    obtenerToken()
      .then((token) => perfilApi.obtener(token))
      .then((perfil) => vigente && setMias(perfil.carrerasIds))
      .catch(() => vigente && setMias([]));
    return () => {
      vigente = false;
    };
  }, [obtenerToken]);

  return mias;
}

/**
 * Las opciones de un selector de carrera, con las del perfil primero ("Tus carreras") para no tener que buscarlas
 * entre todas. Si el usuario no cargó ninguna, es la lista de siempre.
 */
export function OpcionesCarrera({ carreras, mias: cargadas }: { carreras: Carrera[] | null | undefined; mias: string[] | null }) {
  if (!carreras) return null;
  const mias = cargadas ?? [];
  const opcion = (c: Carrera) => (
    <option key={c.id} value={c.id}>
      {c.nombre}
    </option>
  );
  const propias = carreras.filter((c) => mias.includes(c.id));
  if (propias.length === 0) return <>{carreras.map(opcion)}</>;

  return (
    <>
      <optgroup label="Tus carreras">{propias.map(opcion)}</optgroup>
      <optgroup label="Otras carreras">{carreras.filter((c) => !mias.includes(c.id)).map(opcion)}</optgroup>
    </>
  );
}
