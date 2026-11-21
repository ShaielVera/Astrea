"use client";

import { useEffect } from "react";

/** Alto máximo de la lista abierta (igual que en globals.css: 20rem) y alto aproximado de cada opción. */
const ALTO_MAXIMO = 320;
const ALTO_OPCION = 36;
const MARGEN = 16;

/**
 * Las listas de los desplegables se abren siempre hacia abajo. Si al abrir una no entra entera debajo del campo,
 * la página baja lo justo para mostrarla completa (y si la página no tiene más para bajar, se le agrega espacio abajo).
 * Si entra, no se mueve nada.
 */
export function ListasDesplegables() {
  useEffect(() => {
    function alAbrir(evento: PointerEvent) {
      if (evento.button !== 0 || !(evento.target instanceof Element)) return;
      const campo = evento.target.closest("select");
      if (!campo || campo.disabled || campo.multiple) return;

      const opciones = campo.querySelectorAll("option, optgroup").length;
      const altoLista = Math.min(ALTO_MAXIMO, opciones * ALTO_OPCION + 8);
      const faltante = altoLista + MARGEN - (window.innerHeight - campo.getBoundingClientRect().bottom);
      if (faltante <= 0) return;

      const raiz = document.documentElement;
      const puedeBajar = raiz.scrollHeight - window.innerHeight - window.scrollY;
      if (puedeBajar < faltante) {
        const actual = parseFloat(getComputedStyle(document.body).paddingBottom) || 0;
        document.body.style.paddingBottom = `${actual + faltante - puedeBajar}px`;
      }
      window.scrollBy({ top: faltante, behavior: "instant" });
    }

    // En la fase de captura: corre antes de que el navegador abra la lista.
    document.addEventListener("pointerdown", alAbrir, true);
    return () => document.removeEventListener("pointerdown", alAbrir, true);
  }, []);

  return null;
}

/** Campos que abren el teclado del celular (los tildes, radios, fechas y botones no). */
function abreTeclado(elemento: Element | null): elemento is HTMLElement {
  if (elemento instanceof HTMLTextAreaElement) return true;
  if (!(elemento instanceof HTMLInputElement)) return false;
  return ["text", "search", "email", "tel", "url", "number", "password"].includes(elemento.type);
}

/**
 * En el celular, al abrirse el teclado el campo que se está escribiendo queda a la vista: si el teclado lo tapa
 * (o la página se quedó mostrando lo de arriba), la página baja lo justo para centrarlo en lo que queda de pantalla.
 */
export function CampoVisibleConTeclado() {
  useEffect(() => {
    const pantalla = window.visualViewport;
    if (!pantalla) return;
    const visor = pantalla;

    function mostrarCampo() {
      const campo = document.activeElement;
      if (!abreTeclado(campo)) return;
      const { top, bottom } = campo.getBoundingClientRect();
      const arriba = visor.offsetTop;
      const abajo = visor.offsetTop + visor.height;
      if (top >= arriba + 8 && bottom <= abajo - 8) return;
      campo.scrollIntoView({ block: "center", behavior: "instant" });
    }

    // El teclado tarda en abrirse: se revisa cuando la pantalla visible cambia de alto, y un rato después de enfocar.
    let espera: ReturnType<typeof setTimeout> | undefined;
    function alEnfocar(evento: FocusEvent) {
      if (!abreTeclado(evento.target as Element)) return;
      clearTimeout(espera);
      espera = setTimeout(mostrarCampo, 350);
    }

    visor.addEventListener("resize", mostrarCampo);
    document.addEventListener("focusin", alEnfocar);
    return () => {
      clearTimeout(espera);
      visor.removeEventListener("resize", mostrarCampo);
      document.removeEventListener("focusin", alEnfocar);
    };
  }, []);

  return null;
}
