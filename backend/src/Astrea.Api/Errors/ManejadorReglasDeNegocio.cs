using Astrea.Application.Abstractions.Persistence;
using Astrea.Domain.Common;
using Microsoft.AspNetCore.Diagnostics;

namespace Astrea.Api.Errors;

/// <summary>
/// Una regla de negocio violada (DomainException) es un error del cliente: 400 con Problem Details.
/// Un conflicto de concurrencia optimista (dos cambios a la vez sobre el mismo registro) es un 409.
/// Cualquier otra excepción sigue siendo un 500 sin detalles internos.
/// </summary>
internal sealed class ManejadorReglasDeNegocio(IProblemDetailsService problemDetails) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        var (status, titulo, codigo) = exception switch
        {
            DomainException => (StatusCodes.Status400BadRequest, "La solicitud no cumple una regla de negocio.", "regla_de_negocio"),
            ConflictoDeConcurrenciaException => (StatusCodes.Status409Conflict, "Conflicto de concurrencia.", "conflicto_concurrencia"),
            RegistroDuplicadoException => (StatusCodes.Status409Conflict, "Registro duplicado.", "registro_duplicado"),
            _ => (0, string.Empty, string.Empty),
        };

        if (status == 0)
        {
            return false;
        }

        httpContext.Response.StatusCode = status;
        return await problemDetails.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails =
            {
                Status = status,
                Title = titulo,
                Detail = exception.Message,
                Extensions = { ["code"] = codigo },
            },
        });
    }
}
