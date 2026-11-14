using Astrea.Domain.Common;
using Microsoft.AspNetCore.Diagnostics;

namespace Astrea.Api.Errors;

/// <summary>
/// Una regla de negocio violada (DomainException) es un error del cliente: 400 con Problem Details.
/// Cualquier otra excepción sigue siendo un 500 sin detalles internos.
/// </summary>
internal sealed class ManejadorReglasDeNegocio(IProblemDetailsService problemDetails) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        if (exception is not DomainException reglaViolada)
        {
            return false;
        }

        httpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
        return await problemDetails.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails =
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "La solicitud no cumple una regla de negocio.",
                Detail = reglaViolada.Message,
                Extensions = { ["code"] = "regla_de_negocio" },
            },
        });
    }
}
