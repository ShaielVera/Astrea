const modulos = [
  {
    titulo: "Correlatividades",
    descripcion: "Qué necesitás regularizado para cursar y aprobado para rendir cada materia.",
  },
  {
    titulo: "Mesas de examen",
    descripcion: "Llamados, fechas, horarios y profesores, filtrables por fecha y materia.",
  },
  {
    titulo: "Mis trámites",
    descripcion: "Pedí constancias y seguí su estado hasta que estén listas para descargar.",
  },
  {
    titulo: "Hallazgos",
    descripcion: "Objetos perdidos encontrados en el instituto y dónde están resguardados.",
  },
  {
    titulo: "Asistente",
    descripcion: "Consultá en lenguaje natural sobre reglamentos, mesas y trámites.",
  },
];

export default function Inicio() {
  return (
    <main className="mx-auto flex w-full max-w-5xl flex-1 flex-col gap-10 px-4 py-12 sm:px-6">
      <header className="flex flex-col items-start gap-6 rounded-2xl border border-dorado-500/30 bg-gradient-to-br from-dorado-100/60 to-superficie p-6 sm:flex-row sm:items-center sm:p-8 dark:from-indigo-900/60 dark:to-indigo-950">
        {/* eslint-disable-next-line @next/next/no-img-element -- SVG estático, no necesita optimización */}
        <img src="/icons/astrea.svg" alt="" width={112} height={112} className="size-24 shrink-0 drop-shadow-md sm:size-28" />
        <div className="flex flex-col gap-2">
          <p className="text-xs font-semibold uppercase tracking-[0.18em] text-dorado-700 dark:text-dorado-300">
            Instituto Superior de Profesorado N.º 20
          </p>
          <h1 className="text-4xl font-semibold tracking-tight sm:text-5xl">Astrea</h1>
          <p className="max-w-2xl text-lg text-zinc-600 dark:text-zinc-400">
            Portal académico e institucional: correlatividades, mesas de examen, trámites,
            objetos perdidos y un asistente para resolver tus consultas.
          </p>
        </div>
      </header>

      <section aria-labelledby="modulos-titulo" className="flex flex-col gap-4">
        <h2 id="modulos-titulo" className="text-xl font-semibold">
          Módulos
        </h2>
        <ul className="grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
          {modulos.map((modulo) => (
            <li
              key={modulo.titulo}
              className="flex flex-col gap-2 rounded-xl border border-zinc-200 bg-superficie p-5 shadow-sm dark:border-zinc-800"
            >
              <h3 className="flex items-center gap-2 font-medium text-indigo-900 dark:text-indigo-100">
                <span aria-hidden="true" className="text-dorado-500">✦</span>
                {modulo.titulo}
              </h3>
              <p className="text-sm text-zinc-600 dark:text-zinc-400">{modulo.descripcion}</p>
            </li>
          ))}
        </ul>
      </section>
    </main>
  );
}
