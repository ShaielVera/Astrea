"use client";

import { useEffect } from "react";

/** Instala el service worker (solo en producción: en desarrollo cachear assets confunde el recargado en caliente). */
export function RegistrarServiceWorker() {
  useEffect(() => {
    if (process.env.NODE_ENV !== "production" || !("serviceWorker" in navigator)) return;
    navigator.serviceWorker.register("/sw.js", { scope: "/", updateViaCache: "none" }).catch(() => {
      // Sin service worker la app funciona igual; solo no hay página sin conexión.
    });
  }, []);

  return null;
}
