"use client";

import { useRouter } from "next/navigation";
import { useEffect } from "react";
import { useAuth } from "@/features/auth/AuthProvider";

/** En la portada: si ya hay una sesión abierta, lleva directo al inicio con el saludo y los avisos. */
export function IrAlInicioConSesion() {
  const { sesion, perfil } = useAuth();
  const router = useRouter();

  useEffect(() => {
    if (sesion.estado === "autenticado" && perfil) router.replace("/inicio");
  }, [sesion.estado, perfil, router]);

  return null;
}
