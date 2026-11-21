"use client";

import Link from "next/link";
import { useEffect, useState } from "react";
import { useAuth } from "@/features/auth/AuthProvider";
import { tramitesApi, type TramiteResumen } from "./api";

/** En este navegador, los avisos que el estudiante ya cerró ("Entendido"). Solo ids de trámites, nada personal. */
const CLAVE_VISTOS = "astrea.avisos-tramites-vistos";

function leerVistos(): string[] {
  try {
    return JSON.parse(localStorage.getItem(CLAVE_VISTOS) ?? "[]") as string[];
  } catch {
    return [];
  }
}

function marcarVisto(id: string) {
  try {
    localStorage.setItem(CLAVE_VISTOS, JSON.stringify([...new Set([...leerVistos(), id])].slice(-50)));
  } catch {
    // Sin almacenamiento (modo privado): el aviso se vuelve a mostrar la próxima vez, nada más.
  }
}

/** "Constancia de alumno regular" → "Tu constancia de alumno regular está lista para retirar." */
function mensaje(tramite: TramiteResumen): string {
  const nombre = tramite.tipoNombre.charAt(0).toLowerCase() + tramite.tipoNombre.slice(1);
  const lista = /^certificado/i.test(tramite.tipoNombre) ? "listo" : "lista";
  return `Tu ${nombre} está ${lista} para retirar.`;
}

/**
 * Avisos en el inicio cuando Secretaría aprueba un trámite (queda "Listo"). Se ven hasta que el estudiante
 * toca "Entendido". Todo queda dentro de Astrea: no se manda ningún email ni SMS.
 */
export function AvisosDeTramites() {
  const { obtenerToken } = useAuth();
  const [listos, setListos] = useState<TramiteResumen[]>([]);

  useEffect(() => {
    let vigente = true;
    obtenerToken()
      .then((token) => tramitesApi.mios(token))
      .then((pagina) => {
        if (!vigente) return;
        const vistos = leerVistos();
        setListos(pagina.items.filter((t) => t.estado === "Listo" && !vistos.includes(t.id)));
      })
      .catch(() => undefined);
    return () => {
      vigente = false;
    };
  }, [obtenerToken]);

  if (listos.length === 0) return null;

  return (
    <ul aria-label="Avisos de tus trámites" className="flex flex-col gap-2">
      {listos.map((tramite) => (
        <li
          key={tramite.id}
          role="status"
          className="flex flex-wrap items-center justify-between gap-3 rounded-xl border border-emerald-600/30 bg-emerald-50 px-4 py-3 text-sm text-emerald-950 dark:bg-emerald-950/60 dark:text-emerald-100"
        >
          <span className="flex items-center gap-2">
            <svg aria-hidden="true" viewBox="0 0 24 24" className="size-5 shrink-0 text-emerald-600" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
              <path d="M20 6 9 17l-5-5" />
            </svg>
            <span className="font-semibold">{mensaje(tramite)}</span>
          </span>
          <span className="flex items-center gap-2">
            <Link href="/tramites" className="rounded-full bg-emerald-700 px-3.5 py-1.5 font-semibold text-white hover:bg-emerald-800">
              Ver detalle
            </Link>
            <button
              type="button"
              onClick={() => {
                marcarVisto(tramite.id);
                setListos((actuales) => actuales.filter((t) => t.id !== tramite.id));
              }}
              className="rounded-full border border-emerald-700/40 px-3.5 py-1.5 font-semibold hover:bg-emerald-100 dark:hover:bg-emerald-900"
            >
              Entendido
            </button>
          </span>
        </li>
      ))}
    </ul>
  );
}
