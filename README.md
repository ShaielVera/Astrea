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
Desarrollar una aplicación web moderna (con soporte PWA instalable) basada en Slices Verticales, utilizando **C# (.NET) con Arquitectura Hexagonal** en el backend y **Next.js** en el frontend, integrada con **Firebase Auth** y **Firebase Storage**, asistida por un Agente de IA con RAG para resolver consultas institucionales en lenguaje natural.

---

## 🧩 Alcance (visión general)
- **Módulo Académico:** Consulta interactiva de planes de estudio y árboles de correlatividades para cursar y rendir.
- **Calendario y Mesas de Examen:** Fechas, horarios y tribunales filtrables por cátedra.
- **Gestión de Trámites:** Solicitud estudiantil de constancias y generación automática de PDFs oficiales firmados/con QR por la administración.
- **Objetos Perdidos ("Hallazgos"):** Sistema comunitario de reportes con punto de resguardo y validación presencial de identidad para su cierre.
- **Asistente Inteligente (RAG):** Chat con IA basado en la indexación vectorial de normativas, reglamentos y planes de estudio oficiales.

---

## ✅ Requerimientos funcionales

**1. Módulo de Correlatividades y Consultas**
- **1.1** Selección dinámica de carrera y año.
- **1.2** Visualización clara de requisitos aprobados (para final) y regularizados (para cursada).
- **1.3** Calendario de mesas de examen.

**2. Gestión de Trámites**
- **2.1** Solicitud digital de certificados (alumno regular, examen, etc.).
- **2.2** Bandeja de entrada para el Administrador (Bedelía/Secretaría) con aprobación y generación instantánea de PDFs.
- **2.3** Seguimiento de estados (`Pendiente`, `En Proceso`, `Rechazado`, `Listo`). Un trámite rechazado muestra el motivo al estudiante.

**3. Objetos Perdidos ("Hallazgos")**
- **3.1** Formulario de reporte con carga de imagen (vía Firebase Storage), aula, horario y sector de resguardo.
- **3.2** Listado público seguro (ocultando datos sensibles).
- **3.3** Cierre de caso por el administrador con registro de DNI de quien retira.

**4. Asistente con Inteligencia Artificial (Agente RAG)**
- **4.1** Ingesta de documentos oficiales en PDF y sincronización institucional.
- **4.2** Base de datos vectorial (`pgvector`) para consultas semánticas precisas sin alucinaciones.
- **4.3** Respuestas con fuentes citadas (documento, versión y página, o datos actuales de Astrea); si no hay evidencia, lo reconoce y deriva a Secretaría.
- **4.4** Herramientas de solo lectura (correlatividades, mesas, hallazgos y mis trámites) y propuestas de trámite que solo se envían cuando el estudiante confirma.
- **4.5** Panel de métricas para Secretaría: frecuencia de preguntas, temas sin respuesta y errores de la IA, con datos anónimos.

---

## 🏗️ Criterios de calidad y arquitectura
- **Backend:** C# con .NET 10 LTS bajo **Arquitectura Hexagonal (Ports and Adapters)**, APIs paginadas y testing estricto (unitario, integración y end-to-end con Playwright).
- **Frontend:** Next.js (React) con Tailwind CSS, estructurado por Slices Verticales.
- **Seguridad:** Autenticación mediante Firebase Auth y validación de JWT en el backend.
- **Almacenamiento:** Firebase Storage para archivos multimedia.
- **Escalabilidad y Trazabilidad:** Diseño modular preparado para auditorías de acciones críticas.

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

## 🔖 Convenciones del repositorio
- **Ramas:** `main` (estable) · `feature/<nombre>` por funcionalidad. Se integra frecuentemente a `main` mediante pull requests pequeños.
- **Commits:** Mensajes claros y en imperativo (ej.: `feat: implementa modulo de tramites`).

---

## 🚀 Cómo contribuir
1. Crear una rama de trabajo `feature/<nombre-corto>`.
2. Desarrollar el Slice Vertical correspondiente junto a sus pruebas de testing.
3. Enviar Pull Request a `main` con breve resumen funcional.

---

## 🧑‍🏫 Créditos
Proyecto educativo de **Prácticas Profesionalizantes 2** — ISP Nº20 (2026).  
Estudiante: **Brenda Shaiel Vera** (TSDS & TSSITI).  
Docente a cargo: **Alejandro Ramírez**. 💙