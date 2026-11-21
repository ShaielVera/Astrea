"use client";

import {
  GoogleAuthProvider,
  createUserWithEmailAndPassword,
  onAuthStateChanged,
  sendPasswordResetEmail,
  signInWithEmailAndPassword,
  signInWithPopup,
  signOut,
  type User,
} from "firebase/auth";
import { FirebaseError } from "firebase/app";
import { createContext, useCallback, useContext, useEffect, useMemo, useState, type ReactNode } from "react";
import { ApiError, apiFetch } from "@/lib/api-client";
import { obtenerAuth } from "@/lib/firebase/client";
import { mensajeDeError } from "./mensajes-error";
import type { Perfil } from "./tipos";

type EstadoSesion =
  | { estado: "cargando" }
  | { estado: "anonimo" }
  | { estado: "autenticado"; usuario: User; perfil: Perfil }
  /** La cuenta de Firebase sigue abierta pero la API no respondió: se reintenta sola, sin cerrar la sesión. */
  | { estado: "sinServidor"; usuario: User; mensaje: string };

type AuthContextValue = {
  sesion: EstadoSesion;
  perfil: Perfil | null;
  /** Motivo por el que la API rechazó la última sesión (por ejemplo, cuenta desactivada). */
  errorSesion: string | null;
  ingresarConEmail: (email: string, password: string) => Promise<void>;
  crearCuenta: (email: string, password: string) => Promise<void>;
  ingresarConGoogle: () => Promise<void>;
  /** Firebase manda un correo (gratis) con un enlace para elegir una contraseña nueva. */
  recuperarPassword: (email: string) => Promise<void>;
  cerrarSesion: () => Promise<void>;
  /** Vuelve a conectar con la API cuando no respondió. */
  reintentar: () => void;
  /** ID Token vigente para llamar a la API. Firebase lo renueva solo cuando vence. */
  obtenerToken: () => Promise<string>;
};

const AuthContext = createContext<AuthContextValue | null>(null);

export function AuthProvider({ children }: { children: ReactNode }) {
  const [sesion, setSesion] = useState<EstadoSesion>({ estado: "cargando" });
  const [errorSesion, setErrorSesion] = useState<string | null>(null);

  /** Vincula la cuenta de Firebase con el usuario institucional de Astrea. */
  const vincular = useCallback(async (usuario: User) => {
    try {
      const perfil = await apiFetch<Perfil>("/auth/sesion", {
        method: "POST",
        token: await usuario.getIdToken(),
      });
      setErrorSesion(null);
      setSesion({ estado: "autenticado", usuario, perfil });
    } catch (error) {
      if (esRechazoDeLaCuenta(error)) {
        // Firebase aceptó la cuenta pero Astrea no (por ejemplo, está desactivada): se cierra la sesión y se informa el motivo.
        setErrorSesion(mensajeDeError(error));
        await signOut(obtenerAuth());
        return;
      }

      // El servidor no respondió (reinicio, sin conexión): la sesión sigue abierta y se reintenta.
      setSesion({ estado: "sinServidor", usuario, mensaje: mensajeDeError(error) });
    }
  }, []);

  useEffect(() => {
    return onAuthStateChanged(obtenerAuth(), async (usuario) => {
      if (!usuario) {
        setSesion({ estado: "anonimo" });
        return;
      }

      setSesion({ estado: "cargando" });
      await vincular(usuario);
    });
  }, [vincular]);

  // Reintento automático mientras el servidor no responde.
  useEffect(() => {
    if (sesion.estado !== "sinServidor") return;
    const espera = setTimeout(() => void vincular(sesion.usuario), REINTENTO_MS);
    return () => clearTimeout(espera);
  }, [sesion, vincular]);

  const reintentar = useCallback(() => {
    const usuario = obtenerAuth().currentUser;
    if (!usuario) return;
    setSesion({ estado: "cargando" });
    void vincular(usuario);
  }, [vincular]);

  const ingresarConEmail = useCallback(async (email: string, password: string) => {
    setErrorSesion(null);
    await signInWithEmailAndPassword(obtenerAuth(), email, password);
  }, []);

  const crearCuenta = useCallback(async (email: string, password: string) => {
    setErrorSesion(null);
    await createUserWithEmailAndPassword(obtenerAuth(), email, password);
  }, []);

  const ingresarConGoogle = useCallback(async () => {
    setErrorSesion(null);
    await signInWithPopup(obtenerAuth(), new GoogleAuthProvider());
  }, []);

  const recuperarPassword = useCallback(async (email: string) => {
    const auth = obtenerAuth();
    auth.languageCode = "es";
    try {
      await sendPasswordResetEmail(auth, email);
    } catch (error) {
      // No se avisa si el email no tiene cuenta: así nadie puede averiguar qué emails están registrados.
      if (error instanceof FirebaseError && error.code === "auth/user-not-found") return;
      throw error;
    }
  }, []);

  const cerrarSesion = useCallback(async () => {
    await signOut(obtenerAuth());
  }, []);

  const obtenerToken = useCallback(async () => {
    const usuario = obtenerAuth().currentUser;
    if (!usuario) throw new Error("No hay una sesión iniciada.");
    return usuario.getIdToken();
  }, []);

  const valor = useMemo<AuthContextValue>(
    () => ({
      sesion,
      perfil: sesion.estado === "autenticado" ? sesion.perfil : null,
      errorSesion,
      ingresarConEmail,
      crearCuenta,
      ingresarConGoogle,
      recuperarPassword,
      cerrarSesion,
      reintentar,
      obtenerToken,
    }),
    [sesion, errorSesion, ingresarConEmail, crearCuenta, ingresarConGoogle, recuperarPassword, cerrarSesion, reintentar, obtenerToken],
  );

  return <AuthContext.Provider value={valor}>{children}</AuthContext.Provider>;
}

const REINTENTO_MS = 5000;

/**
 * La API rechazó la cuenta de forma definitiva (401, 403 o 404: token inválido, cuenta desactivada…).
 * Los errores de red, los 5xx y los límites de uso (429) son pasajeros: no cierran la sesión.
 */
function esRechazoDeLaCuenta(error: unknown): boolean {
  return error instanceof ApiError && error.status >= 400 && error.status < 500 && error.status !== 408 && error.status !== 429;
}

export function useAuth(): AuthContextValue {
  const contexto = useContext(AuthContext);
  if (!contexto) throw new Error("useAuth debe usarse dentro de <AuthProvider>.");
  return contexto;
}
