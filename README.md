# ⚖️ Astrea — Portal Académico e Institucional (2026)

**Carrera:** Técnico Superior en Desarrollo de Software (TSDS) & Técnico Superior en Redes e Infraestructura de Tecnologías de la Información (TSSITI)  
**Cátedra:** Prácticas Profesionalizantes 2  
**Año:** 2026  
**Institución:** Instituto Superior de Profesorado Nº20  
**Autora:** Brenda Shaiel Vera  
**Profesor:** Alejandro Ramírez

> Plataforma web institucional para la **gestión integral de trámites, correlatividades, objetos perdidos y consultas asistidas por Inteligencia Artificial**, optimizando la vida administrativa y estudiantil del instituto. ✨

---

## 🏛️ ¿Por qué se llama "Astrea"?
**Astrea** es la diosa griega de la justicia, el orden y la precisión. Este nombre fue seleccionado porque encaja perfectamente con los pilares fundamentales del sistema:
- **Precisión y Orden:** En la validación estricta de correlatividades por carrera y año.
- **Justicia y Verificación:** En el cierre seguro de trámites burocráticos y la entrega verificada de objetos perdidos a sus legítimos dueños.

---

## 🎯 Objetivo
Desarrollar una aplicación web moderna (con soporte PWA instalable) basada en Slices Verticales, utilizando **C# (.NET) con Arquitectura Hexagonal** en el backend y **Next.js** en el frontend, integrada con **Firebase Auth** y **PostgreSQL + pgvector**, asistida por un Agente de IA (Gemini) con RAG para resolver consultas institucionales en lenguaje natural.

---

## 🧩 Alcance (visión general)
- **Módulo Académico:** Consulta interactiva de planes de estudio y árboles de correlatividades para cursar y rendir.
- **Mesas de Examen:** Llamados con fecha, horario, aula y tribunal, filtrables por fecha, carrera y materia.
- **Gestión de Trámites:** Solicitud estudiantil de constancias y generación automática de PDFs oficiales con código QR verificable, al aprobarlas la administración.
- **Objetos Perdidos ("Hallazgos"):** Sistema comunitario de reportes con punto de resguardo y validación presencial de identidad para su cierre.
- **Asistente Inteligente (RAG):** Chat con IA basado en la indexación vectorial de normativas, reglamentos y planes de estudio oficiales.

---

## ✅ Requerimientos funcionales

**1. Módulo de Correlatividades y Consultas**
- **1.1** Selección dinámica de carrera y año.
- **1.2** Visualización clara de requisitos aprobados (para final) y regularizados (para cursada).
- **1.3** Listado de mesas de examen con filtros por fecha, carrera y materia.

**2. Gestión de Trámites**
- **2.1** Solicitud digital de constancias (de alumno regular y de examen), también desde el asistente. El estudiante puede descartar el pedido antes de enviarlo o cancelarlo mientras no esté listo.
- **2.2** Bandeja de entrada para la Secretaría con aprobación y generación instantánea del PDF, con un código QR que cualquiera puede validar y una huella SHA-256 que detecta adulteraciones.
- **2.3** Seguimiento de estados (`Pendiente`, `En Proceso`, `Rechazado`, `Listo`, `Cancelado`). Un trámite rechazado muestra el motivo al estudiante.

**3. Objetos Perdidos ("Hallazgos")**
- **3.1** Formulario de reporte con foto (con vista previa para quitarla si se eligió otra por error, validada en el servidor y guardada en almacenamiento privado), lugar, fecha y punto de resguardo.
- **3.2** Listado público seguro (ocultando datos sensibles).
- **3.3** Cierre de caso por la Secretaría con registro de quién retira (nombre y DNI), quién verificó y cuándo. Los datos de quien retira se borran a los 30 días.

**4. Asistente con Inteligencia Artificial (Agente RAG)**
- **4.1** Ingesta de documentos oficiales en PDF (reglamentos, planes, cronogramas) cargados por la Secretaría, con versiones.
- **4.2** Base de datos vectorial (`pgvector`) para búsquedas semánticas: el asistente responde solo con lo que encuentra en los documentos y datos oficiales.
- **4.3** Respuestas con fuentes citadas (documento, versión y página, o datos actuales de Astrea); si no hay evidencia, lo reconoce y deriva a Secretaría.
- **4.4** Herramientas de solo lectura (plan de estudios por año, correlatividades, mesas, hallazgos y mis trámites; reconoce las siglas de las carreras, como TSDS) y propuestas de trámite que solo se envían cuando el estudiante confirma.
- **4.5** Panel de métricas para Secretaría: frecuencia de preguntas, temas sin respuesta y errores de la IA, con datos anónimos.

**5. Confirmación de asistencia (por ejemplo, para la beca Progresar)**
- **5.1** Rol Docente: Secretaría asigna a cada profesor las materias que dicta, y solo ve a los estudiantes de esas materias.
- **5.2** El estudiante anota las materias que cursa en el año y ve lo que confirmaron sus profesores.
- **5.3** La dirección pide una confirmación con fecha de cierre y cada profesor marca si el estudiante asiste regularmente, de forma irregular o no asiste. La dirección sigue el avance por materia.
- **5.4** El informe se filtra con la lista de DNI de la beca (se pega completa, por ejemplo desde Excel), que Astrea no guarda, y se descarga en CSV. El sistema informa; la decisión es de la dirección.

**6. Mi perfil**
- **6.1** El estudiante completa nombre, apellido y DNI; Secretaría los confirma antes de imprimirlos en una constancia y, desde entonces, solo ella los cambia.
- **6.2** Email y celular de contacto validados con un código de 6 dígitos (vence a los 10 minutos, 5 intentos, se guarda solo su hash). La validación dura un año y después se pide renovarla.

---

## 🏗️ Criterios de calidad y arquitectura
- **Backend:** C# con .NET 10 LTS bajo **Arquitectura Hexagonal (Ports and Adapters)**, APIs versionadas (`/api/v1`) y paginadas. Un test de arquitectura verifica que el dominio no dependa de nada externo.
- **Frontend:** Next.js (React) con Tailwind CSS, organizado por funcionalidad. Es responsive e instalable como PWA.
- **Base de datos:** PostgreSQL con `pgvector` y migraciones de Entity Framework Core.
- **Seguridad:** autenticación con Firebase Auth. El backend valida cada token y toma los roles de su propia base, nunca del cliente.
- **Almacenamiento de archivos:** PDFs emitidos, fotos de hallazgos y documentos del asistente se guardan en una carpeta privada del servidor, fuera de la web pública. Solo se descargan a través de la API, con permiso.
- **Inteligencia Artificial:** Google Gemini para embeddings y respuestas, detrás de una interfaz propia, por lo que se puede cambiar de proveedor. Para los tests hay un modelo local determinista, sin red.
- **Trazabilidad:** cada cambio de estado de un trámite queda registrado con quién lo hizo y cuándo, y cada entrega de un hallazgo registra quién verificó la identidad.
- **Testing:** unitarios (xUnit), integración contra PostgreSQL real en Docker (Testcontainers) y end-to-end con Playwright en escritorio y celular. Se ejecutan en GitHub Actions en cada push a `main`.

---

## 🛡️ Seguridad, privacidad y operación
- **Permisos auditados:** un test recorre todos los endpoints de la API y falla si alguno de administración no exige el rol, o si aparece un endpoint público nuevo.
- **Sin acceso cruzado (IDOR):** cada estudiante ve solo sus trámites, documentos y conversaciones; un recurso ajeno responde igual que uno inexistente.
- **Límites de uso** en el inicio de sesión, el asistente, la publicación de hallazgos y la validación pública de documentos.
- **Cabeceras de seguridad** y respuestas de la API sin caché; cada pedido lleva un id de correlación que aparece en los logs y en los mensajes de error.
- **Privacidad:** los DNI, emails y teléfonos se ocultan antes de guardar una pregunta o enviarla a la IA. Las conversaciones se borran a los 30 días y las métricas son anónimas.
- **PWA instalable:** la app se puede instalar en el celular o en la computadora. Sin conexión muestra una página fija, porque nunca se guardan datos personales en el dispositivo.
- **Respaldo:** hay scripts para respaldar y restaurar la base de datos y los archivos privados, con verificación de integridad (`infra/backup`).
- **Accesibilidad:** se puede navegar con el teclado (con un enlace para saltar al contenido), los formularios tienen etiquetas y cada gráfico tiene una vista en tabla.

---

## 📦 Estado del proyecto
Desarrollado en 14 slices verticales, cada uno con sus tests unitarios, de integración y E2E:

| Slice | Contenido |
|---|---|
| 0–1 | Base del proyecto, CI, inicio de sesión con Firebase y roles |
| 2–3 | Plan de estudios y correlatividades |
| 4 | Mesas de examen |
| 5–6 | Trámites y documentos PDF con QR de validación |
| 7 | Hallazgos (objetos perdidos) |
| 8–10 | Asistente con IA: ingesta de documentos, respuestas con fuentes y herramientas con datos de Astrea |
| 11 | Panel de métricas, endurecimiento de seguridad, PWA, accesibilidad y respaldo |
| 12 | Confirmación de asistencia: rol docente, inscripción a cursada, relevamientos e informe para la dirección |
| 13 | Identidad visual (logo y paleta institucional), Mi perfil con email y celular validados, y mejoras de uso |

---

## 🔖 Convenciones del repositorio
- **Ramas:** cada slice se desarrolló completo, con sus tests, y se integró a `main` en su propio commit. Para trabajo futuro se recomienda una rama `feature/<nombre>` y un pull request a `main`.
- **Commits:** mensajes en español que nombran el slice y lo que agrega (ej.: `feat: slice 4 - mesas de examen` o `Slice 7: hallazgos (objetos perdidos)`).
- **Integración continua:** GitHub Actions verifica el formato, compila y corre todos los tests en cada push y pull request a `main`.

---

## 🚀 Cómo contribuir
1. Crear una rama de trabajo `feature/<nombre-corto>`.
2. Desarrollar el Slice Vertical correspondiente junto a sus pruebas (unitarias, de integración y E2E).
3. Enviar un Pull Request a `main` con un breve resumen funcional; la CI tiene que pasar.


---

## 🧑‍🏫 Créditos
Proyecto educativo de **Prácticas Profesionalizantes 2** — ISP Nº20 (2026).  
Estudiante: **Brenda Shaiel Vera** (TSDS & TSSITI).  
Docente a cargo: **Alejandro Ramírez**. 💙