using Astrea.Api.Authorization;
using Astrea.Application.Contracts.Pagination;
using Astrea.Application.Features.Knowledge;
using Astrea.Domain.Common;
using Astrea.Domain.Knowledge;
using Microsoft.AspNetCore.Mvc;

namespace Astrea.Api.Endpoints;

internal static class DocumentoRagEndpoints
{
    public static RouteGroupBuilder MapDocumentoRagEndpoints(this RouteGroupBuilder api)
    {
        // Solo la Secretaría: documentos institucionales que va a consultar el asistente.
        var documentos = api.MapGroup("/admin/documentos-rag")
            .RequireAuthorization(Politicas.Administrador)
            .WithTags("Administración: documentos del asistente")
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        documentos.MapPost("", async (
                [FromForm] string titulo,
                [FromForm] TipoDocumentoRag tipo,
                [FromForm] string? fuente,
                [FromForm] DateOnly? fechaDocumento,
                [FromForm] Guid? nuevaVersionDe,
                IFormFile? archivo,
                AdministracionDocumentosRag servicio,
                CancellationToken ct) =>
            {
                var contenido = await LeerArchivoAsync(archivo, ct);
                var comando = new CargarDocumentoRagCommand(titulo, tipo, fuente, fechaDocumento, nuevaVersionDe);
                return (await servicio.CargarAsync(comando, contenido, ct)).ACreado(documento => $"/api/v1/admin/documentos-rag/{documento.Id}");
            })
            .DisableAntiforgery() // Autenticación por token Bearer, no por cookie: no hay riesgo de CSRF.
            .WithMetadata(new RequestSizeLimitAttribute(DocumentoRag.TamanioMaximoBytes + (1024 * 1024)))
            .Accepts<IFormFile>("multipart/form-data")
            .WithName("CargarDocumentoRag")
            .WithDescription("Formulario multipart: archivo (PDF hasta 20 MB), titulo, tipo, fuente, fechaDocumento y nuevaVersionDe (opcional). Queda Pendiente y se indexa en segundo plano.")
            .Produces<DocumentoRagDto>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status409Conflict);

        documentos.MapGet("", async (EstadoIndexacion[]? estado, string? texto, TipoDocumentoRag? tipo, int? page, int? pageSize, AdministracionDocumentosRag servicio, CancellationToken ct) =>
                (await servicio.ListarAsync(estado, texto, tipo, page, pageSize, ct)).AHttp())
            .WithName("ListarDocumentosRag")
            .WithDescription("Una fila por documento: su versión más nueva.")
            .Produces<PagedResult<DocumentoRagDto>>();

        documentos.MapGet("/{id:guid}", async (Guid id, AdministracionDocumentosRag servicio, CancellationToken ct) =>
                (await servicio.ObtenerAsync(id, ct)).AHttp())
            .WithName("ObtenerDocumentoRag")
            .Produces<DetalleDocumentoRagDto>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        documentos.MapGet("/{id:guid}/fragmentos", async (Guid id, int? page, int? pageSize, AdministracionDocumentosRag servicio, CancellationToken ct) =>
                (await servicio.ListarFragmentosAsync(id, page, pageSize, ct)).AHttp())
            .WithName("FragmentosDeDocumentoRag")
            .Produces<PagedResult<FragmentoDto>>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        documentos.MapGet("/{id:guid}/archivo", async (Guid id, AdministracionDocumentosRag servicio, CancellationToken ct) =>
                (await servicio.DescargarAsync(id, ct)).AArchivo())
            .WithName("DescargarDocumentoRag")
            .Produces(StatusCodes.Status200OK, contentType: "application/pdf")
            .ProducesProblem(StatusCodes.Status404NotFound);

        documentos.MapPost("/{id:guid}/reindexar", async (Guid id, AdministracionDocumentosRag servicio, CancellationToken ct) =>
                (await servicio.ReindexarAsync(id, ct)).AHttp())
            .WithName("ReindexarDocumentoRag")
            .Produces<DocumentoRagDto>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        documentos.MapPost("/{id:guid}/retirar", async (Guid id, AdministracionDocumentosRag servicio, CancellationToken ct) =>
                (await servicio.RetirarAsync(id, ct)).AHttp())
            .WithName("RetirarDocumentoRag")
            .WithDescription("Lo saca de las consultas del asistente. El archivo y el historial se conservan.")
            .Produces<DocumentoRagDto>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        return api;
    }

    private static async Task<byte[]> LeerArchivoAsync(IFormFile? archivo, CancellationToken cancellationToken)
    {
        if (archivo is null || archivo.Length == 0)
        {
            throw new DomainException("Elegí el archivo PDF del documento.");
        }

        if (archivo.Length > DocumentoRag.TamanioMaximoBytes)
        {
            throw new DomainException($"El PDF no puede superar los {DocumentoRag.TamanioMaximoBytes / (1024 * 1024)} MB.");
        }

        using var memoria = new MemoryStream((int)archivo.Length);
        await archivo.CopyToAsync(memoria, cancellationToken);
        return memoria.ToArray();
    }
}
