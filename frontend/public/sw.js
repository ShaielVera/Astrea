/*
 * Service worker de Astrea. Estrategia de caché segura:
 *  - Solo se guardan archivos estáticos públicos: los JS/CSS versionados de Next (/_next/static), los íconos y la
 *    página "Sin conexión".
 *  - NUNCA se guardan páginas HTML, respuestas de la API (que está en otro origen y además responde "no-store"),
 *    tokens, documentos ni respuestas del asistente: sin red solo se ve la página "Sin conexión".
 */
const VERSION = "astrea-v1";
const PAGINA_SIN_CONEXION = "/sin-conexion";
const PRECARGA = [PAGINA_SIN_CONEXION, "/icons/icon-192.png", "/icons/icon-512.png"];

self.addEventListener("install", (evento) => {
  evento.waitUntil(
    caches
      .open(VERSION)
      .then((cache) => cache.addAll(PRECARGA))
      .then(() => self.skipWaiting()),
  );
});

self.addEventListener("activate", (evento) => {
  evento.waitUntil(
    caches
      .keys()
      .then((claves) => Promise.all(claves.filter((clave) => clave !== VERSION).map((clave) => caches.delete(clave))))
      .then(() => self.clients.claim()),
  );
});

self.addEventListener("fetch", (evento) => {
  const pedido = evento.request;
  if (pedido.method !== "GET") return;

  const url = new URL(pedido.url);
  // Otro origen (la API, Firebase, Google): el navegador lo maneja sin intervención.
  if (url.origin !== self.location.origin) return;

  // Navegación: siempre de la red; sin red, la página fija "Sin conexión".
  if (pedido.mode === "navigate") {
    evento.respondWith(fetch(pedido).catch(() => caches.match(PAGINA_SIN_CONEXION)));
    return;
  }

  // Archivos estáticos versionados (el nombre cambia con cada build) e íconos: primero la caché.
  if (url.pathname.startsWith("/_next/static/") || url.pathname.startsWith("/icons/")) {
    evento.respondWith(
      caches.match(pedido).then(
        (guardada) =>
          guardada ||
          fetch(pedido).then((respuesta) => {
            if (respuesta.ok && respuesta.type === "basic") {
              const copia = respuesta.clone();
              caches.open(VERSION).then((cache) => cache.put(pedido, copia));
            }
            return respuesta;
          }),
      ),
    );
  }
  // Todo lo demás va a la red sin guardarse.
});
