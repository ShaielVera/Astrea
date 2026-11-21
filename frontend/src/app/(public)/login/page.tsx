"use client";

import { useRouter } from "next/navigation";
import { useEffect, useState, type FormEvent } from "react";
import { useAuth } from "@/features/auth/AuthProvider";
import { mensajeDeError } from "@/features/auth/mensajes-error";
import { ServidorNoDisponible } from "@/features/auth/RequiereRol";
import { areaInicial } from "@/features/auth/tipos";

type Modo = "ingresar" | "crear" | "recuperar";

const TITULO: Record<Modo, string> = {
  ingresar: "Ingresar a Astrea",
  crear: "Crear una cuenta",
  recuperar: "Recuperar tu contraseña",
};

const BAJADA: Record<Modo, string> = {
  ingresar: "Si ya tenés una cuenta en Astrea, ingresá con tu email y contraseña o con Google.",
  crear: "Es tu primera vez: elegí tu email y una contraseña de al menos 6 caracteres. Las cuentas nuevas comienzan como Estudiante.",
  recuperar: "Escribí el email de tu cuenta y te mandamos un enlace para elegir una contraseña nueva.",
};

const botonSecundario =
  "rounded-md border border-zinc-300 px-4 py-2 font-medium hover:bg-zinc-100 disabled:opacity-60 dark:border-zinc-700 dark:hover:bg-zinc-800";
const enlace = "text-sm font-medium text-indigo-600 hover:underline dark:text-indigo-400";

export default function LoginPage() {
  const { sesion, perfil, errorSesion, ingresarConEmail, crearCuenta, ingresarConGoogle, recuperarPassword } = useAuth();
  const router = useRouter();
  const [modo, setModo] = useState<Modo>("ingresar");
  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [verPassword, setVerPassword] = useState(false);
  const [enviando, setEnviando] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [aviso, setAviso] = useState<string | null>(null);

  useEffect(() => {
    if (sesion.estado === "autenticado" && perfil) router.replace(areaInicial(perfil));
  }, [sesion.estado, perfil, router]);

  function cambiarModo(nuevo: Modo) {
    setModo(nuevo);
    setError(null);
    setAviso(null);
  }

  async function ejecutar(accion: () => Promise<void>) {
    setError(null);
    setAviso(null);
    setEnviando(true);
    try {
      await accion();
    } catch (e) {
      setError(mensajeDeError(e));
    } finally {
      setEnviando(false);
    }
  }

  function enviar(evento: FormEvent<HTMLFormElement>) {
    evento.preventDefault();
    if (modo === "recuperar") {
      void ejecutar(async () => {
        await recuperarPassword(email.trim());
        setAviso(`Listo. Si hay una cuenta con ${email.trim()}, te llega un correo con el enlace en unos minutos. Revisá también Spam o Promociones.`);
      });
      return;
    }
    void ejecutar(() => (modo === "ingresar" ? ingresarConEmail(email, password) : crearCuenta(email, password)));
  }

  const ocupado = enviando || sesion.estado === "cargando" || sesion.estado === "autenticado";
  const mensaje = error ?? errorSesion;

  // Ya hay una cuenta abierta: no se pide ingresar de nuevo, solo esperar a que vuelva el servidor.
  if (sesion.estado === "sinServidor") {
    return <ServidorNoDisponible mensaje={sesion.mensaje} />;
  }

  return (
    <main className="mx-auto flex w-full max-w-sm flex-1 flex-col gap-6 px-4 py-12">
      <div className="flex flex-col gap-1">
        <h1 className="text-2xl font-semibold">{TITULO[modo]}</h1>
        <p className="text-sm text-zinc-600 dark:text-zinc-400">{BAJADA[modo]}</p>
      </div>

      <form onSubmit={enviar} aria-label={TITULO[modo]} className="flex flex-col gap-4" noValidate>
        <label className="flex flex-col gap-1 text-sm font-medium">
          Email
          <input
            type="email"
            name="email"
            autoComplete="email"
            required
            value={email}
            onChange={(e) => setEmail(e.target.value)}
            className="rounded-md border border-zinc-300 bg-transparent px-3 py-2 font-normal dark:border-zinc-700"
          />
        </label>

        {modo !== "recuperar" && (
          <div className="flex flex-col gap-1 text-sm font-medium">
            <label htmlFor="password">Contraseña</label>
            <div className="relative">
              <input
                id="password"
                type={verPassword ? "text" : "password"}
                name="password"
                autoComplete={modo === "ingresar" ? "current-password" : "new-password"}
                required
                minLength={6}
                value={password}
                onChange={(e) => setPassword(e.target.value)}
                className="w-full rounded-md border border-zinc-300 bg-transparent py-2 pr-11 pl-3 font-normal dark:border-zinc-700"
              />
              <button
                type="button"
                onClick={() => setVerPassword((actual) => !actual)}
                aria-label={verPassword ? "Ocultar contraseña" : "Mostrar contraseña"}
                aria-pressed={verPassword}
                title={verPassword ? "Ocultar contraseña" : "Mostrar contraseña"}
                className="absolute inset-y-0 right-0 flex w-11 items-center justify-center rounded-r-md text-zinc-500 hover:text-zinc-800 dark:hover:text-zinc-200"
              >
                <svg aria-hidden="true" viewBox="0 0 24 24" className="size-5" fill="none" stroke="currentColor" strokeWidth="1.8" strokeLinecap="round" strokeLinejoin="round">
                  <path d="M2 12s3.5-7 10-7 10 7 10 7-3.5 7-10 7S2 12 2 12z" />
                  <circle cx="12" cy="12" r="3" />
                  {verPassword && <path d="M3 3l18 18" />}
                </svg>
              </button>
            </div>
            {modo === "ingresar" && (
              <button type="button" onClick={() => cambiarModo("recuperar")} className={`${enlace} self-end`}>
                Olvidé mi contraseña
              </button>
            )}
          </div>
        )}

        {mensaje && (
          <p role="alert" className="rounded-md bg-red-50 px-3 py-2 text-sm text-red-700 dark:bg-red-950 dark:text-red-300">
            {mensaje}
          </p>
        )}
        {aviso && (
          <p role="status" className="rounded-md bg-emerald-50 px-3 py-2 text-sm text-emerald-900 dark:bg-emerald-950 dark:text-emerald-200">
            {aviso}
          </p>
        )}

        <button
          type="submit"
          disabled={ocupado}
          className="rounded-md bg-indigo-600 px-4 py-2 font-medium text-white hover:bg-indigo-700 disabled:opacity-60"
        >
          {modo === "ingresar" ? "Ingresar" : modo === "crear" ? "Crear cuenta" : "Enviarme el enlace"}
        </button>
      </form>

      {modo === "recuperar" ? (
        <>
          <p className="text-sm text-zinc-600 dark:text-zinc-400">
            Si entrás con Google no tenés una contraseña de Astrea: volvé y tocá “Continuar con Google”.
          </p>
          <button type="button" onClick={() => cambiarModo("ingresar")} className={enlace}>
            Volver a ingresar
          </button>
        </>
      ) : (
        <>
          <div className="flex items-center gap-3 text-xs text-zinc-500">
            <span className="h-px flex-1 bg-zinc-200 dark:bg-zinc-800" />o<span className="h-px flex-1 bg-zinc-200 dark:bg-zinc-800" />
          </div>

          <button type="button" disabled={ocupado} onClick={() => void ejecutar(ingresarConGoogle)} className={botonSecundario}>
            Continuar con Google
          </button>

          {modo === "ingresar" ? (
            <section aria-labelledby="primera-vez" className="flex flex-col gap-2 rounded-xl border border-dorado-500/40 bg-dorado-100/60 p-4 dark:bg-indigo-900/50">
              <h2 id="primera-vez" className="font-semibold text-indigo-950 dark:text-indigo-100">
                ¿Es tu primera vez en Astrea?
              </h2>
              <p className="text-sm text-zinc-700 dark:text-zinc-300">
                Si nunca ingresaste, creá tu cuenta con tu email. Con Google no hace falta: tu cuenta se crea sola la primera vez.
              </p>
              <button type="button" onClick={() => cambiarModo("crear")} className={`${botonSecundario} bg-white dark:bg-zinc-900`}>
                Crear mi cuenta
              </button>
            </section>
          ) : (
            <button type="button" onClick={() => cambiarModo("ingresar")} className={enlace}>
              ¿Ya tenés cuenta? Ingresá
            </button>
          )}
        </>
      )}
    </main>
  );
}
