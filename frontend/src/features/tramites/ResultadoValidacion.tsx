"use client";

import Link from "next/link";
import { useParams } from "next/navigation";
import { useEffect, useState } from "react";
import { ApiError } from "@/lib/api-client";
import { mensajeDeError } from "@/features/auth/mensajes-error";
import { fechaHora, tramitesApi, type ValidacionDocumento } from "./api";

type Estado = { tipo: "cargando" } | { tipo: "valido"; documento: ValidacionDocumento } | { tipo: "inexistente" } | { tipo: "error"; mensaje: string };

/** Resultado de validar el código de un QR. Es público: muestra solo datos mínimos del documento. */
export function ResultadoValidacion() {
  const { codigo } = useParams<{ codigo: string }>();
  const [estado, setEstado] = useState<Estado>({ tipo: "cargando" });

  useEffect(() => {
    let vigente = true;
    tramitesApi
      .validar(decodeURIComponent(codigo))
      .then((documento) => vigente && setEstado({ tipo: "valido", documento }))
      .catch((e) => {
        if (!vigente) return;
        setEstado(e instanceof ApiError && e.status === 404 ? { tipo: "inexistente" } : { tipo: "error", mensaje: mensajeDeError(e) });
      });
    return () => {
      vigente = false;
    };
  }, [codigo]);

  if (estado.tipo === "cargando") {
    return (
      <p role="status" className="text-zinc-500">
        Verificando el documento…
      </p>
    );
  }

  if (estado.tipo === "error") {
    return (
      <p role="alert" className="rounded-md bg-red-50 px-3 py-2 text-red-700 dark:bg-red-950 dark:text-red-300">
        {estado.mensaje}
      </p>
    );
  }

  if (estado.tipo === "inexistente") {
    return (
      <section aria-label="Resultado de la validación" className="flex flex-col gap-2 rounded-xl border border-red-300 bg-red-50 p-5 dark:border-red-900 dark:bg-red-950/40">
        <h2 className="text-xl font-semibold text-red-800 dark:text-red-200">✗ No es un documento de Astrea</h2>
        <p className="text-sm">No existe ningún documento emitido con el código {decodeURIComponent(codigo)}. Revisá que esté bien escrito.</p>
        <Link href="/validar" className="text-sm font-medium text-indigo-700 hover:underline dark:text-indigo-300">
          Ingresar otro código
        </Link>
      </section>
    );
  }

  const { documento } = estado;
  const vigente = documento.estado === "Vigente";

  return (
    <section
      aria-label="Resultado de la validación"
      className={`flex flex-col gap-3 rounded-xl border p-5 ${
        vigente ? "border-emerald-300 bg-emerald-50 dark:border-emerald-900 dark:bg-emerald-950/40" : "border-amber-300 bg-amber-50 dark:border-amber-900 dark:bg-amber-950/40"
      }`}
    >
      <h2 className={`text-xl font-semibold ${vigente ? "text-emerald-800 dark:text-emerald-200" : "text-amber-800 dark:text-amber-200"}`}>
        {vigente ? "✓ Documento auténtico y vigente" : "⚠ Documento auténtico, pero anulado"}
      </h2>
      {!vigente && <p className="text-sm">Fue emitido por el instituto pero se anuló después: ya no tiene validez.</p>}
      <dl className="grid grid-cols-1 gap-x-4 gap-y-1 text-sm sm:grid-cols-[11rem_1fr]">
        <dt className="text-zinc-500">Documento</dt>
        <dd>{documento.tipoDocumento}</dd>
        <dt className="text-zinc-500">Código</dt>
        <dd className="font-mono">{documento.codigoValidacion}</dd>
        <dt className="text-zinc-500">Trámite</dt>
        <dd>{documento.numeroTramite}</dd>
        <dt className="text-zinc-500">Emitido</dt>
        <dd>{fechaHora(documento.fechaEmision)}</dd>
        {documento.dniParcial && (
          <>
            <dt className="text-zinc-500">DNI del titular</dt>
            <dd>{documento.dniParcial}</dd>
          </>
        )}
        <dt className="text-zinc-500">Huella SHA-256</dt>
        <dd className="break-all font-mono text-xs">{documento.hashSha256}</dd>
      </dl>
      <p className="text-xs text-zinc-500">
        Por privacidad se muestran solo los últimos dígitos del DNI. Comparalos con el documento que te presentaron.
      </p>
    </section>
  );
}
