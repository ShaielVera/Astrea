import type { ReactNode } from "react";

/** Los módulos de Astrea: los mismos en el inicio del estudiante y en la portada pública (así no quedan distintos). */
// Íconos de trazo simple (24×24), en la línea de los de Atenea.
export const ICONOS: Record<string, ReactNode> = {
  plan: <path d="M4 19.5A2.5 2.5 0 0 1 6.5 17H20V3H6.5A2.5 2.5 0 0 0 4 5.5zM4 19.5A2.5 2.5 0 0 0 6.5 22H20v-5M8 7.5h8M8 11h5" />,
  correlatividades: (
    <>
      <circle cx="6" cy="6" r="2.5" />
      <circle cx="18" cy="18" r="2.5" />
      <path d="M8.5 6H14a4 4 0 0 1 4 4v5.5" />
    </>
  ),
  mesas: (
    <>
      <rect x="3" y="4.5" width="18" height="17" rx="2" />
      <path d="M8 2.5v4M16 2.5v4M3 10h18M8.5 15l2 2 4-4" />
    </>
  ),
  tramites: (
    <>
      <path d="M14 2.5H6.5A2 2 0 0 0 4.5 4.5v15a2 2 0 0 0 2 2h11a2 2 0 0 0 2-2V8z" />
      <path d="M14 2.5V8h5.5M8.5 13h7M8.5 17h5" />
    </>
  ),
  cursada: (
    <>
      <path d="M16 21v-2a4 4 0 0 0-4-4H6a4 4 0 0 0-4 4v2" />
      <circle cx="9" cy="7" r="4" />
      <path d="m16 11 2 2 4-4" />
    </>
  ),
  hallazgos: (
    <>
      <circle cx="11" cy="11" r="7" />
      <path d="m20.5 20.5-4.5-4.5" />
    </>
  ),
  perfil: (
    <>
      <circle cx="12" cy="8" r="4" />
      <path d="M4 21a8 8 0 0 1 16 0" />
    </>
  ),
  asistente: <path d="M21 15a2 2 0 0 1-2 2H7l-4 4V5a2 2 0 0 1 2-2h14a2 2 0 0 1 2 2zM8 9h8M8 13h5" />,
};

export const MODULOS = [
  { titulo: "Mi perfil", descripcion: "Tus datos para las constancias, y tu email y celular validados.", href: "/mi-perfil", icono: "perfil" },
  { titulo: "Plan de estudios", descripcion: "Todas las materias de tu carrera, ordenadas año por año y por cuatrimestre.", href: "/plan-de-estudios", icono: "plan" },
  { titulo: "Correlatividades", descripcion: "Antes de anotarte, mirá qué materias tenés que tener regularizadas o aprobadas.", href: "/correlatividades", icono: "correlatividades" },
  { titulo: "Mesas de examen", descripcion: "Fechas, horarios, aulas y profesores de cada llamado a examen final.", href: "/mesas", icono: "mesas" },
  { titulo: "Mis trámites", descripcion: "Pedí tus constancias sin hacer fila y enterate cuando estén listas para descargar.", href: "/tramites", icono: "tramites" },
  { titulo: "Mi cursada", descripcion: "Anotá las materias que cursás y mirá tu asistencia confirmada.", href: "/mi-cursada", icono: "cursada" },
  { titulo: "Hallazgos", descripcion: "¿Perdiste algo en el instituto? Mirá lo que se encontró y dónde retirarlo.", href: "/hallazgos", icono: "hallazgos" },
  { titulo: "Asistente", descripcion: "Preguntale como a Secretaría: te responde con los reglamentos y datos oficiales, y te dice de dónde lo sacó.", href: "/asistente", icono: "asistente" },
];
