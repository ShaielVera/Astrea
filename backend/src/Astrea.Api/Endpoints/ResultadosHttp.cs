using Astrea.Application.Contracts.Results;
using Astrea.Application.Features.Procedures;

namespace Astrea.Api.Endpoints;

/// <summary>Traduce los resultados de los casos de uso a respuestas HTTP con Problem Details.</summary>
internal static class ResultadosHttp
{
    public static IResult AHttp<T>(this Result<T> resultado) =>
        resultado.EsExitoso ? Results.Ok(resultado.Valor) : resultado.Error!.AProblema();

    public static IResult ACreado<T>(this Result<T> resultado, Func<T, string> ubicacion) =>
        resultado.EsExitoso ? Results.Created(ubicacion(resultado.Valor), resultado.Valor) : resultado.Error!.AProblema();

    public static IResult ASinContenido<T>(this Result<T> resultado) =>
        resultado.EsExitoso ? Results.NoContent() : resultado.Error!.AProblema();

    /// <summary>Archivo para descargar; "private, no-store" para que ningún proxy ni caché guarde un documento personal.</summary>
    public static IResult AArchivo(this Result<ArchivoDto> resultado)
    {
        if (!resultado.EsExitoso)
        {
            return resultado.Error!.AProblema();
        }

        return new ArchivoPrivado(resultado.Valor);
    }

    private sealed class ArchivoPrivado(ArchivoDto archivo) : IResult
    {
        public Task ExecuteAsync(HttpContext httpContext)
        {
            httpContext.Response.Headers.CacheControl = "private, no-store";
            return Results.File(archivo.Contenido, archivo.ContentType, archivo.NombreArchivo).ExecuteAsync(httpContext);
        }
    }

    public static IResult AProblema(this Error error)
    {
        var status = error.Tipo switch
        {
            TipoError.Validacion => StatusCodes.Status400BadRequest,
            TipoError.NoAutenticado => StatusCodes.Status401Unauthorized,
            TipoError.Prohibido => StatusCodes.Status403Forbidden,
            TipoError.NoEncontrado => StatusCodes.Status404NotFound,
            TipoError.Conflicto => StatusCodes.Status409Conflict,
            TipoError.NoDisponible => StatusCodes.Status503ServiceUnavailable,
            _ => StatusCodes.Status500InternalServerError,
        };

        return Results.Problem(
            statusCode: status,
            detail: error.Mensaje,
            extensions: new Dictionary<string, object?> { ["code"] = error.Codigo });
    }
}
