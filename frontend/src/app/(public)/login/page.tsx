"use client";

import { useRouter } from "next/navigation";
import { useEffect, useState, type FormEvent } from "react";
import { useAuth } from "@/features/auth/AuthProvider";
import { mensajeDeError } from "@/features/auth/mensajes-error";
import { ServidorNoDisponible } from "@/features/auth/RequiereRol";
import { areaInicial } from "@/features/auth/tipos";

type Modo = "ingresar" | "crear";

export default function LoginPage() {
  const { sesion, perfil, errorSesion, ingresarConEmail, crearCuenta, ingresarConGoogle } = useAuth();
  const router = useRouter();
  const [modo, setModo] = useState<Modo>("ingresar");
  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [verPassword, setVerPassword] = useState(false);
  const [enviando, setEnviando] = useState(false);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    if (sesion.estado === "autenticado" && perfil) router.replace(areaInicial(perfil));
  }, [sesion.estado, perfil, router]);

  async function ejecutar(accion: () => Promise<void>) {
    setError(null);
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
        <h1 className="text-2xl font-semibold">{modo === "ingresar" ? "Ingresar a Astrea" : "Crear una cuenta"}</h1>
        <p className="text-sm text-zinc-600 dark:text-zinc-400">
          {modo === "ingresar"
            ? "Usá tu email y contraseña o tu cuenta de Google."
            : "Las cuentas nuevas comienzan como Estudiante."}
        </p>
      </div>

      <form onSubmit={enviar} className="flex flex-col gap-4" noValidate>
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
        </div>

        {mensaje && (
          <p role="alert" className="rounded-md bg-red-50 px-3 py-2 text-sm text-red-700 dark:bg-red-950 dark:text-red-300">
            {mensaje}
          </p>
        )}

        <button
          type="submit"
          disabled={ocupado}
          className="rounded-md bg-indigo-600 px-4 py-2 font-medium text-white hover:bg-indigo-700 disabled:opacity-60"
        >
          {modo === "ingresar" ? "Ingresar" : "Crear cuenta"}
        </button>
      </form>

      <div className="flex items-center gap-3 text-xs text-zinc-500">
        <span className="h-px flex-1 bg-zinc-200 dark:bg-zinc-800" />o<span className="h-px flex-1 bg-zinc-200 dark:bg-zinc-800" />
      </div>

      <button
        type="button"
        disabled={ocupado}
        onClick={() => void ejecutar(ingresarConGoogle)}
        className="rounded-md border border-zinc-300 px-4 py-2 font-medium hover:bg-zinc-100 disabled:opacity-60 dark:border-zinc-700 dark:hover:bg-zinc-800"
      >
        Continuar con Google
      </button>

      <button
        type="button"
        onClick={() => {
          setModo(modo === "ingresar" ? "crear" : "ingresar");
          setError(null);
        }}
        className="text-sm font-medium text-indigo-600 hover:underline dark:text-indigo-400"
      >
        {modo === "ingresar" ? "¿No tenés cuenta? Creá una" : "¿Ya tenés cuenta? Ingresá"}
      </button>
    </main>
  );
}
