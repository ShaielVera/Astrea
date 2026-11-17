"use client";

import { useEffect, useState, type FormEvent } from "react";
import { useAuth } from "@/features/auth/AuthProvider";
import { mensajeDeError } from "@/features/auth/mensajes-error";
import { fechaCorta } from "@/features/asistencia/api";
import { celularLegible, perfilApi, type Canal, type CodigoEnviado, type Contacto, type MiPerfil } from "@/features/perfil/api";

const tarjeta = "flex flex-col gap-4 rounded-xl border border-zinc-200 bg-superficie p-5 shadow-sm dark:border-zinc-800";
const campo = "w-full min-w-0 rounded-md border border-zinc-300 bg-transparent px-3 py-2 font-normal dark:border-zinc-700 dark:bg-zinc-900";
const botonPrimario = "rounded-md bg-indigo-600 px-4 py-2 font-medium text-white hover:bg-indigo-700 disabled:opacity-60";
const botonSecundario =
  "rounded-md border border-zinc-300 px-3 py-1.5 text-sm font-medium hover:bg-zinc-100 disabled:opacity-60 dark:border-zinc-700 dark:hover:bg-zinc-800";

export default function MiPerfilPage() {
  const { obtenerToken } = useAuth();
  const [perfil, setPerfil] = useState<MiPerfil | null>(null);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    let vigente = true;
    obtenerToken()
      .then((token) => perfilApi.obtener(token))
      .then((resultado) => vigente && setPerfil(resultado))
      .catch((e) => vigente && setError(mensajeDeError(e)));
    return () => {
      vigente = false;
    };
  }, [obtenerToken]);

  return (
    <main className="mx-auto flex w-full max-w-3xl flex-1 flex-col gap-6 px-4 py-10 sm:px-6">
      <div className="flex flex-col gap-1">
        <h1 className="text-3xl font-semibold tracking-tight">Mi perfil</h1>
        <p className="text-zinc-600 dark:text-zinc-400">
          Tus datos van impresos en las constancias, y el email y el celular son por donde el instituto se comunica con vos. La validación
          dura un año: después te vamos a pedir que la renueves por si cambiaste de número o de email.
        </p>
      </div>

      {error && (
        <p role="alert" className="rounded-md bg-red-50 px-4 py-2 text-red-800 dark:bg-red-950 dark:text-red-200">
          {error}
        </p>
      )}
      {!perfil && !error && (
        <p role="status" className="text-zinc-500">
          Cargando…
        </p>
      )}

      {perfil && (
        <>
          <DatosPersonales perfil={perfil} onGuardado={setPerfil} />
          <DatosDeContacto key={`${perfil.emailContacto.valor}-${perfil.celular.valor}`} perfil={perfil} onGuardado={setPerfil} />
        </>
      )}
    </main>
  );
}

function DatosPersonales({ perfil, onGuardado }: { perfil: MiPerfil; onGuardado: (perfil: MiPerfil) => void }) {
  const { obtenerToken } = useAuth();
  const [nombre, setNombre] = useState(perfil.nombre ?? "");
  const [apellido, setApellido] = useState(perfil.apellido ?? "");
  const [dni, setDni] = useState(perfil.dni ?? "");
  const [estado, setEstado] = useState<{ tipo: "ok" | "error"; mensaje: string } | null>(null);
  const [guardando, setGuardando] = useState(false);

  async function guardar(evento: FormEvent<HTMLFormElement>) {
    evento.preventDefault();
    setGuardando(true);
    setEstado(null);
    try {
      onGuardado(await perfilApi.guardarDatos(await obtenerToken(), { nombre, apellido, dni }));
      setEstado({ tipo: "ok", mensaje: "Guardado. Secretaría los va a confirmar cuando revise tu próximo trámite." });
    } catch (e) {
      setEstado({ tipo: "error", mensaje: mensajeDeError(e) });
    } finally {
      setGuardando(false);
    }
  }

  return (
    <section aria-labelledby="datos-titulo" className={tarjeta}>
      <div className="flex flex-wrap items-center justify-between gap-2">
        <h2 id="datos-titulo" className="text-lg font-semibold">
          Datos personales
        </h2>
        {perfil.datosConfirmados ? (
          <Etiqueta tono="ok">Confirmados por Secretaría</Etiqueta>
        ) : perfil.dni ? (
          <Etiqueta tono="aviso">Falta que Secretaría los confirme</Etiqueta>
        ) : (
          <Etiqueta tono="aviso">Incompletos</Etiqueta>
        )}
      </div>

      {perfil.datosConfirmados ? (
        <>
          <dl className="grid grid-cols-1 gap-x-4 gap-y-1 text-sm sm:grid-cols-[8rem_1fr]">
            <dt className="text-zinc-500">Nombre</dt>
            <dd>{perfil.nombre}</dd>
            <dt className="text-zinc-500">Apellido</dt>
            <dd>{perfil.apellido}</dd>
            <dt className="text-zinc-500">DNI</dt>
            <dd>{perfil.dni}</dd>
            {perfil.legajo && (
              <>
                <dt className="text-zinc-500">Legajo</dt>
                <dd>{perfil.legajo}</dd>
              </>
            )}
          </dl>
          <p className="text-sm text-zinc-500">Si algún dato está mal, pedí la corrección en Secretaría con tu DNI.</p>
        </>
      ) : (
        <form onSubmit={guardar} aria-label="Datos personales" className="flex flex-col gap-3">
          <p className="text-sm text-zinc-600 dark:text-zinc-400">Escribilos como figuran en tu DNI: así salen en las constancias.</p>
          <div className="grid grid-cols-1 gap-3 sm:grid-cols-2">
            <label className="flex min-w-0 flex-col gap-1 text-sm font-medium">
              Nombre
              <input value={nombre} onChange={(e) => setNombre(e.target.value)} required maxLength={100} autoComplete="given-name" className={campo} />
            </label>
            <label className="flex min-w-0 flex-col gap-1 text-sm font-medium">
              Apellido
              <input value={apellido} onChange={(e) => setApellido(e.target.value)} required maxLength={100} autoComplete="family-name" className={campo} />
            </label>
            <label className="flex min-w-0 flex-col gap-1 text-sm font-medium">
              DNI
              <input value={dni} onChange={(e) => setDni(e.target.value)} required inputMode="numeric" maxLength={12} placeholder="Ej.: 40123456" className={campo} />
            </label>
          </div>
          <div>
            <button type="submit" disabled={guardando} className={botonPrimario}>
              {guardando ? "Guardando…" : "Guardar datos"}
            </button>
          </div>
          <Mensaje estado={estado} />
        </form>
      )}
    </section>
  );
}

function DatosDeContacto({ perfil, onGuardado }: { perfil: MiPerfil; onGuardado: (perfil: MiPerfil) => void }) {
  const { obtenerToken } = useAuth();
  const [email, setEmail] = useState(perfil.emailContacto.valor ?? perfil.emailCuenta);
  const [celular, setCelular] = useState(perfil.celular.valor ? celularLegible(perfil.celular.valor) : "");
  const [estado, setEstado] = useState<{ tipo: "ok" | "error"; mensaje: string } | null>(null);
  const [guardando, setGuardando] = useState(false);

  async function guardar(evento: FormEvent<HTMLFormElement>) {
    evento.preventDefault();
    setGuardando(true);
    setEstado(null);
    try {
      onGuardado(await perfilApi.guardarContacto(await obtenerToken(), { emailContacto: email.trim() || null, celular: celular.trim() || null }));
    } catch (e) {
      setEstado({ tipo: "error", mensaje: mensajeDeError(e) });
    } finally {
      setGuardando(false);
    }
  }

  return (
    <section aria-labelledby="contacto-titulo" className={tarjeta}>
      <h2 id="contacto-titulo" className="text-lg font-semibold">
        Email y celular de contacto
      </h2>
      <p className="text-sm text-zinc-600 dark:text-zinc-400">
        Usá un email y un celular que revises seguido. Te mandamos un código de 6 dígitos para comprobar que son tuyos.
      </p>
      <form onSubmit={guardar} aria-label="Contacto" className="flex flex-col gap-3">
        <div className="grid grid-cols-1 gap-3 sm:grid-cols-2">
          <label className="flex min-w-0 flex-col gap-1 text-sm font-medium">
            Email de contacto
            <input type="email" value={email} onChange={(e) => setEmail(e.target.value)} maxLength={320} autoComplete="email" className={campo} />
          </label>
          <label className="flex min-w-0 flex-col gap-1 text-sm font-medium">
            Celular (con código de área)
            <input
              type="tel"
              value={celular}
              onChange={(e) => setCelular(e.target.value)}
              maxLength={25}
              autoComplete="tel-national"
              placeholder="Ej.: 342 412-3456"
              className={campo}
            />
          </label>
        </div>
        <div>
          <button type="submit" disabled={guardando} className={botonPrimario}>
            {guardando ? "Guardando…" : "Guardar contacto"}
          </button>
        </div>
        <Mensaje estado={estado} />
      </form>

      <div className="grid grid-cols-1 gap-3 sm:grid-cols-2">
        <Validacion canal="email" titulo="Email" contacto={perfil.emailContacto} mostrar={perfil.emailContacto.valor} onValidado={onGuardado} />
        <Validacion
          canal="celular"
          titulo="Celular"
          contacto={perfil.celular}
          mostrar={perfil.celular.valor ? celularLegible(perfil.celular.valor) : null}
          onValidado={onGuardado}
        />
      </div>
    </section>
  );
}

/** Estado de la validación de un dato y el ida y vuelta del código. */
function Validacion({
  canal,
  titulo,
  contacto,
  mostrar,
  onValidado,
}: {
  canal: Canal;
  titulo: string;
  contacto: Contacto;
  mostrar: string | null;
  onValidado: (perfil: MiPerfil) => void;
}) {
  const { obtenerToken } = useAuth();
  const [enviado, setEnviado] = useState<CodigoEnviado | null>(null);
  const [codigo, setCodigo] = useState("");
  const [estado, setEstado] = useState<{ tipo: "ok" | "error"; mensaje: string } | null>(null);
  const [ocupado, setOcupado] = useState(false);
  const vigente = contacto.verificado && !contacto.vencido;

  async function enviar() {
    setOcupado(true);
    setEstado(null);
    try {
      setEnviado(await perfilApi.enviarCodigo(await obtenerToken(), canal));
      setCodigo("");
    } catch (e) {
      setEstado({ tipo: "error", mensaje: mensajeDeError(e) });
    } finally {
      setOcupado(false);
    }
  }

  async function validar(evento: FormEvent<HTMLFormElement>) {
    evento.preventDefault();
    setOcupado(true);
    setEstado(null);
    try {
      onValidado(await perfilApi.validarCodigo(await obtenerToken(), canal, codigo));
    } catch (e) {
      setEstado({ tipo: "error", mensaje: mensajeDeError(e) });
    } finally {
      setOcupado(false);
    }
  }

  return (
    <div aria-label={`Validación del ${titulo.toLowerCase()}`} role="group" className="flex flex-col gap-2 rounded-lg bg-indigo-50/60 p-3 text-sm dark:bg-indigo-950/40">
      <div className="flex flex-wrap items-center justify-between gap-2">
        <span className="font-medium">{titulo}</span>
        {!mostrar ? (
          <Etiqueta tono="neutro">Sin cargar</Etiqueta>
        ) : vigente ? (
          <Etiqueta tono="ok">Validado</Etiqueta>
        ) : contacto.vencido ? (
          <Etiqueta tono="aviso">Validación vencida</Etiqueta>
        ) : (
          <Etiqueta tono="aviso">Sin validar</Etiqueta>
        )}
      </div>
      {mostrar && <span className="break-all text-zinc-600 dark:text-zinc-400">{mostrar}</span>}
      {vigente && contacto.venceEl && <span className="text-xs text-zinc-500">Vence el {fechaCorta(contacto.venceEl)}.</span>}

      {mostrar && !vigente && !enviado && (
        <button type="button" onClick={() => void enviar()} disabled={ocupado} className={`${botonSecundario} self-start`}>
          {ocupado ? "Enviando…" : "Enviar código"}
        </button>
      )}

      {enviado && !vigente && (
        <form onSubmit={validar} aria-label={`Código del ${titulo.toLowerCase()}`} className="flex flex-col gap-2">
          <p className="text-xs text-zinc-600 dark:text-zinc-400">
            {enviado.simulado
              ? "Modo demostración: el código no se mandó, está en la consola del servidor."
              : `Te mandamos un código a ${enviado.destino}. Vence en 10 minutos.`}
          </p>
          <label className="flex flex-col gap-1 font-medium">
            Código de 6 dígitos
            <input
              value={codigo}
              onChange={(e) => setCodigo(e.target.value)}
              required
              inputMode="numeric"
              autoComplete="one-time-code"
              maxLength={7}
              className={`${campo} tracking-[0.3em]`}
            />
          </label>
          <div className="flex flex-wrap gap-2">
            <button type="submit" disabled={ocupado} className={botonSecundario}>
              Validar
            </button>
            <button type="button" onClick={() => void enviar()} disabled={ocupado} className={botonSecundario}>
              Mandar otro código
            </button>
          </div>
        </form>
      )}
      <Mensaje estado={estado} />
    </div>
  );
}

function Etiqueta({ tono, children }: { tono: "ok" | "aviso" | "neutro"; children: string }) {
  const estilos = {
    ok: "bg-emerald-100 text-emerald-900 dark:bg-emerald-950 dark:text-emerald-200",
    aviso: "bg-amber-100 text-amber-900 dark:bg-amber-950 dark:text-amber-200",
    neutro: "bg-zinc-200 text-zinc-700 dark:bg-zinc-800 dark:text-zinc-300",
  };
  return <span className={`rounded-full px-2.5 py-0.5 text-xs font-semibold ${estilos[tono]}`}>{children}</span>;
}

function Mensaje({ estado }: { estado: { tipo: "ok" | "error"; mensaje: string } | null }) {
  if (!estado) return null;
  return (
    <p role={estado.tipo === "ok" ? "status" : "alert"} className={estado.tipo === "ok" ? "text-emerald-700 dark:text-emerald-300" : "text-red-700 dark:text-red-300"}>
      {estado.mensaje}
    </p>
  );
}
