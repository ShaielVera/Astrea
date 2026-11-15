"use client";

import { useState, type FormEvent } from "react";
import type { Carrera } from "@/features/academico/api";
import type { CampoTramite, TramiteTipo } from "./api";

const campo = "w-full min-w-0 rounded-md border border-zinc-300 bg-transparent px-3 py-2 font-normal dark:border-zinc-700 dark:bg-zinc-900";
const botonPrimario = "rounded-md bg-indigo-600 px-4 py-2 font-medium text-white hover:bg-indigo-700 disabled:opacity-60";

type Props = {
  tipo: TramiteTipo;
  carreras: Carrera[];
  onEnviar: (datos: Record<string, string>) => Promise<void>;
};

/** Formulario armado a partir de los campos del tipo de trámite (su "esquema"). */
export function FormularioTramite({ tipo, carreras, onEnviar }: Props) {
  const [valores, setValores] = useState<Record<string, string>>({});
  const [enviando, setEnviando] = useState(false);

  async function enviar(evento: FormEvent<HTMLFormElement>) {
    evento.preventDefault();
    setEnviando(true);
    try {
      await onEnviar(valores);
    } finally {
      setEnviando(false);
    }
  }

  const cambiar = (clave: string, valor: string) => setValores((actuales) => ({ ...actuales, [clave]: valor }));

  return (
    <form onSubmit={enviar} aria-label={tipo.nombre} className="flex flex-col gap-3">
      <p className="text-sm text-zinc-600 dark:text-zinc-400">{tipo.descripcion}</p>
      {tipo.campos.map((definicion) => (
        <Campo key={definicion.clave} definicion={definicion} carreras={carreras} valor={valores[definicion.clave] ?? ""} onCambiar={cambiar} />
      ))}
      <div>
        <button type="submit" className={botonPrimario} disabled={enviando}>
          {enviando ? "Enviando…" : "Enviar solicitud"}
        </button>
      </div>
    </form>
  );
}

function Campo({
  definicion,
  carreras,
  valor,
  onCambiar,
}: {
  definicion: CampoTramite;
  carreras: Carrera[];
  valor: string;
  onCambiar: (clave: string, valor: string) => void;
}) {
  const ayudaId = definicion.ayuda ? `ayuda-${definicion.clave}` : undefined;
  const comunes = {
    value: valor,
    required: definicion.obligatorio,
    "aria-describedby": ayudaId,
    className: campo,
  };

  return (
    <label className="flex min-w-0 flex-col gap-1 text-sm font-medium">
      <span>
        {definicion.etiqueta}
        {!definicion.obligatorio && <span className="font-normal text-zinc-500"> (opcional)</span>}
      </span>
      {definicion.tipo === "Carrera" ? (
        <select {...comunes} onChange={(e) => onCambiar(definicion.clave, e.target.value)}>
          <option value="">Elegí tu carrera</option>
          {carreras.map((carrera) => (
            <option key={carrera.id} value={carrera.id}>
              {carrera.nombre}
            </option>
          ))}
        </select>
      ) : definicion.tipo === "TextoLargo" ? (
        <textarea {...comunes} rows={4} maxLength={definicion.largoMaximo} onChange={(e) => onCambiar(definicion.clave, e.target.value)} />
      ) : (
        <input
          {...comunes}
          type={definicion.tipo === "Fecha" ? "date" : "text"}
          maxLength={definicion.tipo === "Fecha" ? undefined : definicion.largoMaximo}
          onChange={(e) => onCambiar(definicion.clave, e.target.value)}
        />
      )}
      {definicion.ayuda && (
        <span id={ayudaId} className="text-xs font-normal text-zinc-500">
          {definicion.ayuda}
        </span>
      )}
    </label>
  );
}
