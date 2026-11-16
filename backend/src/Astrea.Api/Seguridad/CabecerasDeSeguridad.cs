using System.Text.RegularExpressions;

namespace Astrea.Api.Seguridad;

/// <summary>
/// Endurecimiento de todas las respuestas de la API:
/// <list type="bullet">
/// <item>Cabeceras de seguridad: la API solo devuelve JSON y archivos, así que nada se ejecuta ni se incrusta en un frame.</item>
/// <item>Las respuestas de /api no se guardan en ninguna caché (ni del navegador, ni de un proxy, ni del service worker),
/// porque casi todas tienen datos personales.</item>
/// <item>Un identificador de correlación por pedido (X-Correlation-Id) que aparece en los logs y en los errores,
/// para poder rastrear un problema que reporta un usuario sin pedirle datos personales.</item>
/// </list>
/// </summary>
internal sealed partial class CabecerasDeSeguridad(RequestDelegate next, ILogger<CabecerasDeSeguridad> logger)
{
    public const string CabeceraCorrelacion = "X-Correlation-Id";

    public const string ClaveCorrelacion = "astrea.correlacion";

    public async Task InvokeAsync(HttpContext contexto)
    {
        // Se acepta el id que manda el cliente solo si es corto y seguro para loguear; si no, se genera uno.
        var recibido = contexto.Request.Headers[CabeceraCorrelacion].ToString();
        var correlacion = IdValido().IsMatch(recibido) ? recibido : Guid.NewGuid().ToString("N");
        contexto.Items[ClaveCorrelacion] = correlacion;

        contexto.Response.OnStarting(() =>
        {
            var cabeceras = contexto.Response.Headers;
            cabeceras[CabeceraCorrelacion] = correlacion;
            cabeceras.XContentTypeOptions = "nosniff";
            cabeceras.XFrameOptions = "DENY";
            cabeceras.ContentSecurityPolicy = "default-src 'none'; frame-ancestors 'none'";
            cabeceras["Referrer-Policy"] = "no-referrer";
            cabeceras["Permissions-Policy"] = "camera=(), microphone=(), geolocation=()";
            if (contexto.Request.Path.StartsWithSegments("/api") && string.IsNullOrEmpty(cabeceras.CacheControl))
            {
                cabeceras.CacheControl = "no-store";
            }

            return Task.CompletedTask;
        });

        using (logger.BeginScope(new Dictionary<string, object> { ["CorrelationId"] = correlacion }))
        {
            await next(contexto);
        }
    }

    [GeneratedRegex("^[A-Za-z0-9_-]{8,64}$", RegexOptions.CultureInvariant)]
    private static partial Regex IdValido();
}
