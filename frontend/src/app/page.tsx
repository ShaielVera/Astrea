const modulos = [
  {
    titulo: "Correlatividades",
    descripcion: "Qué necesitás regularizado para cursar y aprobado para rendir cada materia.",
  },
  {
    titulo: "Mesas de examen",
    descripcion: "Llamados, fechas, horarios y tribunales filtrables por fecha y cátedra.",
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
      <header className="flex flex-col gap-3">
        <p className="text-sm font-medium uppercase tracking-wide text-indigo-600 dark:text-indigo-400">
          Instituto Superior de Profesorado N.º 20
        </p>
        <h1 className="text-4xl font-semibold tracking-tight sm:text-5xl">Astrea</h1>
        <p className="max-w-2xl text-lg text-zinc-600 dark:text-zinc-400">
          Portal académico e institucional: correlatividades, mesas de examen, trámites,
          objetos perdidos y un asistente para resolver tus consultas.
        </p>
      </header>

      <section aria-labelledby="modulos-titulo" className="flex flex-col gap-4">
        <h2 id="modulos-titulo" className="text-xl font-semibold">
          Módulos
        </h2>
        <ul className="grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
          {modulos.map((modulo) => (
            <li
              key={modulo.titulo}
              className="flex flex-col gap-2 rounded-xl border border-zinc-200 p-5 dark:border-zinc-800"
            >
              <div className="flex items-center justify-between gap-2">
                <h3 className="font-medium">{modulo.titulo}</h3>
                <span className="rounded-full bg-zinc-100 px-2 py-0.5 text-xs text-zinc-600 dark:bg-zinc-800 dark:text-zinc-300">
                  Próximamente
                </span>
              </div>
              <p className="text-sm text-zinc-600 dark:text-zinc-400">{modulo.descripcion}</p>
            </li>
          ))}
        </ul>
      </section>
    </main>
  );
}
