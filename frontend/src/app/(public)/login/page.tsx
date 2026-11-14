"use client";

import { useRouter } from "next/navigation";
import { useEffect, useState, type FormEvent } from "react";
import { useAuth } from "@/features/auth/AuthProvider";
import { mensajeDeError } from "@/features/auth/mensajes-error";
import { areaInicial } from "@/features/auth/tipos";

type Modo = "ingresar" | "crear";

export default function LoginPage() {
  const { sesion, perfil, errorSesion, ingresarConEmail, crearCuenta, ingresarConGoogle } = useAuth();
  const router = useRouter();
  const [modo, setModo] = useState<Modo>("ingresar");
  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
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
        <label className="flex flex-col gap-1 text-sm font-medium">
          Contraseña
          <input
            type="password"
            name="password"
            autoComplete={modo === "ingresar" ? "current-password" : "new-password"}
            required
            minLength={6}
            value={password}
            onChange={(e) => setPassword(e.target.value)}
            className="rounded-md border border-zinc-300 bg-transparent px-3 py-2 font-normal dark:border-zinc-700"
          />
        </label>

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
