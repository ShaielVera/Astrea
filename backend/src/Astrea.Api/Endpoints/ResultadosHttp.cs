using Astrea.Application.Contracts.Results;

namespace Astrea.Api.Endpoints;

/// <summary>Traduce los resultados de los casos de uso a respuestas HTTP con Problem Details.</summary>
internal static class ResultadosHttp
{
    public static IResult AHttp<T>(this Result<T> resultado) =>
        resultado.EsExitoso ? Results.Ok(resultado.Valor) : resultado.Error!.AProblema();

    public static IResult ACreado<T>(this Result<T> resultado, Func<T, string> ubicacion) =>
        resultado.EsExitoso ? Results.Created(ubicacion(resultado.Valor), resultado.Valor) : resultado.Error!.AProblema();

    public static IResult AProblema(this Error error)
    {
        var status = error.Tipo switch
        {
            TipoError.Validacion => StatusCodes.Status400BadRequest,
            TipoError.NoAutenticado => StatusCodes.Status401Unauthorized,
            TipoError.Prohibido => StatusCodes.Status403Forbidden,
            TipoError.NoEncontrado => StatusCodes.Status404NotFound,
            TipoError.Conflicto => StatusCodes.Status409Conflict,
            _ => StatusCodes.Status500InternalServerError,
        };

        return Results.Problem(
            statusCode: status,
            detail: error.Mensaje,
            extensions: new Dictionary<string, object?> { ["code"] = error.Codigo });
    }
}
