const API_URL = process.env.NEXT_PUBLIC_API_URL ?? "http://localhost:5080/api/v1";

/** Error de la API con el cuerpo Problem Details (RFC 7807) que devuelve el backend. */
export class ApiError extends Error {
  constructor(
    readonly status: number,
    readonly codigo: string | undefined,
    mensaje: string,
  ) {
    super(mensaje);
    this.name = "ApiError";
  }
}

type ProblemDetails = { title?: string; detail?: string; code?: string };

type Opciones = {
  method?: "GET" | "POST" | "PUT" | "PATCH" | "DELETE";
  token?: string;
  body?: unknown;
};

/** Cliente central de la API: agrega el Bearer token y traduce los errores a {@link ApiError}. */
export async function apiFetch<T>(ruta: string, { method = "GET", token, body }: Opciones = {}): Promise<T> {
  const headers: Record<string, string> = { Accept: "application/json" };
  if (token) headers.Authorization = `Bearer ${token}`;
  if (body !== undefined) headers["Content-Type"] = "application/json";

  let respuesta: Response;
  try {
    respuesta = await fetch(`${API_URL}${ruta}`, {
      method,
      headers,
      body: body === undefined ? undefined : JSON.stringify(body),
      cache: "no-store",
    });
  } catch {
    throw new ApiError(0, "red", "No se pudo conectar con el servidor de Astrea.");
  }

  if (!respuesta.ok) {
    const problema = (await respuesta.json().catch(() => ({}))) as ProblemDetails;
    throw new ApiError(
      respuesta.status,
      problema.code,
      problema.detail ?? problema.title ?? `Error ${respuesta.status} al llamar a la API.`,
    );
  }

  // 204 No Content (por ejemplo, al quitar algo): no hay cuerpo que leer.
  if (respuesta.status === 204) return undefined as T;

  return (await respuesta.json()) as T;
}

/**
 * Descarga un archivo protegido (ej. el PDF de un trámite) y lo guarda en el equipo.
 * No se puede usar un enlace común porque el pedido necesita el Bearer token.
 */
export async function apiDescargar(ruta: string, token: string, nombrePorDefecto: string): Promise<void> {
  let respuesta: Response;
  try {
    respuesta = await fetch(`${API_URL}${ruta}`, { headers: { Authorization: `Bearer ${token}` }, cache: "no-store" });
  } catch {
    throw new ApiError(0, "red", "No se pudo conectar con el servidor de Astrea.");
  }

  if (!respuesta.ok) {
    const problema = (await respuesta.json().catch(() => ({}))) as ProblemDetails;
    throw new ApiError(respuesta.status, problema.code, problema.detail ?? problema.title ?? `Error ${respuesta.status} al descargar.`);
  }

  // filename="T-000012-constancia.pdf" (la API lo envía en Content-Disposition).
  const disposicion = respuesta.headers.get("Content-Disposition") ?? "";
  const nombre = /filename\*?=(?:UTF-8'')?"?([^";]+)"?/i.exec(disposicion)?.[1] ?? nombrePorDefecto;

  const url = URL.createObjectURL(await respuesta.blob());
  const enlace = document.createElement("a");
  enlace.href = url;
  enlace.download = decodeURIComponent(nombre);
  document.body.appendChild(enlace);
  enlace.click();
  enlace.remove();
  URL.revokeObjectURL(url);
}
