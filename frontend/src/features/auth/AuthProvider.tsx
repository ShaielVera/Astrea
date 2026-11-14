"use client";

import {
  GoogleAuthProvider,
  createUserWithEmailAndPassword,
  onAuthStateChanged,
  signInWithEmailAndPassword,
  signInWithPopup,
  signOut,
  type User,
} from "firebase/auth";
import { createContext, useCallback, useContext, useEffect, useMemo, useState, type ReactNode } from "react";
import { apiFetch } from "@/lib/api-client";
import { obtenerAuth } from "@/lib/firebase/client";
import { mensajeDeError } from "./mensajes-error";
import type { Perfil } from "./tipos";

type EstadoSesion =
  | { estado: "cargando" }
  | { estado: "anonimo" }
  | { estado: "autenticado"; usuario: User; perfil: Perfil };

type AuthContextValue = {
  sesion: EstadoSesion;
  perfil: Perfil | null;
  /** Motivo por el que la API rechazó la última sesión (por ejemplo, cuenta desactivada). */
  errorSesion: string | null;
  ingresarConEmail: (email: string, password: string) => Promise<void>;
  crearCuenta: (email: string, password: string) => Promise<void>;
  ingresarConGoogle: () => Promise<void>;
  cerrarSesion: () => Promise<void>;
  /** ID Token vigente para llamar a la API. Firebase lo renueva solo cuando vence. */
  obtenerToken: () => Promise<string>;
};

const AuthContext = createContext<AuthContextValue | null>(null);

export function AuthProvider({ children }: { children: ReactNode }) {
  const [sesion, setSesion] = useState<EstadoSesion>({ estado: "cargando" });
  const [errorSesion, setErrorSesion] = useState<string | null>(null);

  useEffect(() => {
    const auth = obtenerAuth();
    return onAuthStateChanged(auth, async (usuario) => {
      if (!usuario) {
        setSesion({ estado: "anonimo" });
        return;
      }

      setSesion({ estado: "cargando" });
      try {
        // Vincula la cuenta de Firebase con el usuario institucional de Astrea.
        const perfil = await apiFetch<Perfil>("/auth/sesion", {
          method: "POST",
          token: await usuario.getIdToken(),
        });
        setErrorSesion(null);
        setSesion({ estado: "autenticado", usuario, perfil });
      } catch (error) {
        // Firebase aceptó la cuenta pero Astrea no: se cierra la sesión y se informa el motivo.
        setErrorSesion(mensajeDeError(error));
        await signOut(auth);
      }
    });
  }, []);

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
      cerrarSesion,
      obtenerToken,
    }),
    [sesion, errorSesion, ingresarConEmail, crearCuenta, ingresarConGoogle, cerrarSesion, obtenerToken],
  );

  return <AuthContext.Provider value={valor}>{children}</AuthContext.Provider>;
}

export function useAuth(): AuthContextValue {
  const contexto = useContext(AuthContext);
  if (!contexto) throw new Error("useAuth debe usarse dentro de <AuthProvider>.");
  return contexto;
}
