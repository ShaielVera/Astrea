"use client";

import { useEffect, useState, type FormEvent } from "react";
import { useAuth } from "@/features/auth/AuthProvider";
import { academicoApi, NOMBRE_ANIO, type Carrera, type MateriaDePlan } from "@/features/academico/api";
import { hoyEnElInstituto } from "@/features/mesas/api";
import type { CampoTramite, TramiteTipo } from "./api";

const campo = "w-full min-w-0 rounded-md border border-zinc-300 bg-transparent px-3 py-2 font-normal dark:border-zinc-700 dark:bg-zinc-900";
const botonPrimario = "rounded-md bg-indigo-600 px-4 py-2 font-medium text-white hover:bg-indigo-700 disabled:opacity-60";

type Props = {
  tipo: TramiteTipo;
  carreras: Carrera[];
  onEnviar: (datos: Record<string, string>) => Promise<void>;
  /** Descarta el formulario sin enviar nada. */
  onCancelar?: () => void;
};

/** Formulario armado a partir de los campos del tipo de trámite (su "esquema"). */
export function FormularioTramite({ tipo, carreras, onEnviar, onCancelar }: Props) {
  // Con una sola carrera en el perfil, ya viene elegida.
  const [valores, setValores] = useState<Record<string, string>>(() =>
    carreras.length === 1 ? Object.fromEntries(tipo.campos.filter((c) => c.tipo === "Carrera").map((c) => [c.clave, carreras[0].id])) : {},
  );
  const [enviando, setEnviando] = useState(false);
  const { obtenerToken } = useAuth();

  // Si el trámite pide una materia (ej.: constancia de examen), se elige de las de la carrera elegida, de todos los años.
  const claveCarrera = tipo.campos.find((c) => c.tipo === "Carrera")?.clave;
  const carreraId = claveCarrera ? valores[claveCarrera] : undefined;
  const pideMateria = tipo.campos.some((c) => c.clave === CLAVE_MATERIA);
  const [materiasDe, setMateriasDe] = useState<{ carreraId: string; lista: MateriaDePlan[] } | null>(null);
  const materias = carreraId && materiasDe?.carreraId === carreraId ? materiasDe.lista : null;

  useEffect(() => {
    if (!pideMateria || !carreraId) return;
    let vigente = true;
    obtenerToken()
      .then(async (token) => {
        const planes = await academicoApi.planes(token, carreraId);
        const plan = planes.find((p) => p.activo) ?? planes[0];
        return plan ? academicoApi.materias(token, plan.id) : [];
      })
      .then((lista) => vigente && setMateriasDe({ carreraId, lista }))
      .catch(() => vigente && setMateriasDe({ carreraId, lista: [] }));
    return () => {
      vigente = false;
    };
  }, [pideMateria, carreraId, obtenerToken]);

  async function enviar(evento: FormEvent<HTMLFormElement>) {
    evento.preventDefault();
    setEnviando(true);
    try {
      await onEnviar(valores);
    } finally {
      setEnviando(false);
    }
  }

  // Al cambiar de carrera, la materia elegida (de la otra carrera) se borra.
  const cambiar = (clave: string, valor: string) =>
    setValores((actuales) => ({ ...actuales, [clave]: valor, ...(clave === claveCarrera && pideMateria ? { [CLAVE_MATERIA]: "" } : {}) }));

  return (
    <form onSubmit={enviar} aria-label={tipo.nombre} className="flex flex-col gap-3">
      <p className="text-sm text-zinc-600 dark:text-zinc-400">{tipo.descripcion}</p>
      {tipo.campos.map((definicion) => (
        definicion.clave === CLAVE_MATERIA ? (
          <SelectorMateria
            key={definicion.clave}
            definicion={definicion}
            materias={materias}
            hayCarrera={Boolean(carreraId)}
            valor={valores[definicion.clave] ?? ""}
            onCambiar={cambiar}
          />
        ) : (
          <Campo key={definicion.clave} definicion={definicion} carreras={carreras} valor={valores[definicion.clave] ?? ""} onCambiar={cambiar} />
        )
      ))}
      <div className="flex flex-wrap gap-3">
        <button type="submit" className={botonPrimario} disabled={enviando}>
          {enviando ? "Enviando…" : "Enviar solicitud"}
        </button>
        {onCancelar && (
          <button
            type="button"
            onClick={onCancelar}
            disabled={enviando}
            className="rounded-md border border-zinc-300 px-4 py-2 font-medium hover:bg-zinc-100 disabled:opacity-60 dark:border-zinc-700 dark:hover:bg-zinc-800"
          >
            Cancelar
          </button>
        )}
      </div>
    </form>
  );
}

const CLAVE_MATERIA = "materia";

/** La materia del trámite, elegida de la lista de la carrera (agrupada por año). Se guarda su nombre, como antes. */
function SelectorMateria({
  definicion,
  materias,
  hayCarrera,
  valor,
  onCambiar,
}: {
  definicion: CampoTramite;
  materias: MateriaDePlan[] | null;
  hayCarrera: boolean;
  valor: string;
  onCambiar: (clave: string, valor: string) => void;
}) {
  const anios = [...new Set((materias ?? []).map((m) => m.anio))].sort((a, b) => a - b);
  const vacio = !hayCarrera ? "Elegí primero la carrera" : materias === null ? "Cargando materias…" : "Elegí la materia";

  return (
    <label className="flex min-w-0 flex-col gap-1 text-sm font-medium">
      <span>{definicion.etiqueta}</span>
      <select
        value={valor}
        required={definicion.obligatorio}
        disabled={!materias}
        onChange={(e) => onCambiar(definicion.clave, e.target.value)}
        className={campo}
      >
        <option value="">{vacio}</option>
        {anios.map((anio) => (
          <optgroup key={anio} label={NOMBRE_ANIO[anio] ?? `Año ${anio}`}>
            {materias
              ?.filter((m) => m.anio === anio)
              .map((m) => (
                <option key={m.planMateriaId} value={m.nombre}>
                  {m.nombre}
                </option>
              ))}
          </optgroup>
        ))}
      </select>
    </label>
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
          max={definicion.tipo === "Fecha" && definicion.sinFechasFuturas ? hoyEnElInstituto() : undefined}
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
