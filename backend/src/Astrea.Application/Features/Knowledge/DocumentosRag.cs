using System.Security.Cryptography;
using Astrea.Application.Abstractions.Ai;
using Astrea.Application.Abstractions.Clock;
using Astrea.Application.Abstractions.Documents;
using Astrea.Application.Abstractions.Persistence;
using Astrea.Application.Contracts.Pagination;
using Astrea.Application.Contracts.Results;
using Astrea.Application.Features.Auth;
using Astrea.Application.Features.Procedures;
using Astrea.Domain.Common;
using Astrea.Domain.Knowledge;

namespace Astrea.Application.Features.Knowledge;

/// <summary>Avisa al procesador en segundo plano que hay documentos nuevos en la cola.</summary>
public interface IAvisoIngesta
{
    void Avisar();
}

/// <summary>
/// La Secretaría carga los documentos institucionales (PDF) que va a consultar el asistente y sigue su indexación.
/// La carga solo guarda el archivo y lo deja Pendiente: el procesamiento pesado lo hace <see cref="IngestaDocumentos"/> en segundo plano.
/// </summary>
public sealed class AdministracionDocumentosRag(
    UsuarioActual usuarioActual,
    IDocumentoRagRepository documentos,
    IFileStorage archivos,
    IUnitOfWork unitOfWork,
    IClock clock,
    IAvisoIngesta aviso)
{
    public async Task<Result<DocumentoRagDto>> CargarAsync(CargarDocumentoRagCommand comando, byte[] contenido, CancellationToken cancellationToken)
    {
        var administrador = await usuarioActual.ObtenerAsync(cancellationToken);
        if (!administrador.EsExitoso)
        {
            return administrador.Error!;
        }

        DocumentoRag.ValidarArchivo(contenido);
        var hash = Convert.ToHexStringLower(SHA256.HashData(contenido));
        if (await documentos.BuscarPorHashAsync(hash, cancellationToken) is { } existente)
        {
            return ErroresDocumentosRag.Duplicado(existente);
        }

        var ahora = clock.UtcNow;
        var id = Guid.CreateVersion7();
        var ruta = $"documentos-rag/{ahora:yyyy}/{id}.pdf";
        var datos = new DatosDocumentoRag(comando.Titulo, comando.Tipo, comando.Fuente, comando.FechaDocumento);

        DocumentoRag documento;
        if (comando.NuevaVersionDe is { } anteriorId)
        {
            var anterior = await documentos.ObtenerAsync(anteriorId, cancellationToken);
            if (anterior is null)
            {
                return ErroresDocumentosRag.NoEncontrado;
            }

            var serie = await documentos.ObtenerSerieAsync(anterior.SerieId, cancellationToken);
            documento = serie[0].NuevaVersion(id, serie[0].Version, datos, ruta, hash, administrador.Valor.Id, ahora);
        }
        else
        {
            documento = DocumentoRag.Cargar(id, datos, ruta, hash, administrador.Valor.Id, ahora);
        }

        documentos.Agregar(documento);
        await archivos.GuardarAsync(ruta, contenido, cancellationToken);
        try
        {
            await unitOfWork.GuardarCambiosAsync(cancellationToken);
        }
        catch
        {
            await archivos.EliminarAsync(ruta, CancellationToken.None);
            throw;
        }

        aviso.Avisar();
        return (await ADtosAsync([documento], cancellationToken))[0];
    }

    public async Task<Result<PagedResult<DocumentoRagDto>>> ListarAsync(EstadoIndexacion[]? estados, string? texto, TipoDocumentoRag? tipo, int? page, int? pageSize, CancellationToken cancellationToken)
    {
        texto = string.IsNullOrWhiteSpace(texto) ? null : texto.Trim();
        if (texto?.Length > ErroresDocumentosRag.LargoMaximoTexto)
        {
            return ErroresDocumentosRag.TextoDemasiadoLargo;
        }

        var pagina = PageRequest.Create(page, pageSize);
        var encontrados = await documentos.ListarAsync(new FiltroDocumentosRag(estados is { Length: > 0 } ? estados : null, texto, tipo), pagina, cancellationToken);
        return PagedResult<DocumentoRagDto>.From(await ADtosAsync(encontrados.Items, cancellationToken), pagina, encontrados.TotalItems);
    }

    public async Task<Result<DetalleDocumentoRagDto>> ObtenerAsync(Guid id, CancellationToken cancellationToken)
    {
        var documento = await documentos.ObtenerAsync(id, cancellationToken);
        if (documento is null)
        {
            return ErroresDocumentosRag.NoEncontrado;
        }

        var versiones = await ADtosAsync(await documentos.ObtenerSerieAsync(documento.SerieId, cancellationToken), cancellationToken);
        return new DetalleDocumentoRagDto(versiones.Single(version => version.Id == id), versiones);
    }

    public async Task<Result<PagedResult<FragmentoDto>>> ListarFragmentosAsync(Guid id, int? page, int? pageSize, CancellationToken cancellationToken)
    {
        if (await documentos.ObtenerAsync(id, cancellationToken) is null)
        {
            return ErroresDocumentosRag.NoEncontrado;
        }

        return await documentos.ListarFragmentosAsync(id, PageRequest.Create(page, pageSize), cancellationToken);
    }

    /// <summary>Vuelve a ponerlo en la cola (después de un error, o para regenerar los fragmentos).</summary>
    public async Task<Result<DocumentoRagDto>> ReindexarAsync(Guid id, CancellationToken cancellationToken)
    {
        var documento = await documentos.ObtenerAsync(id, cancellationToken);
        if (documento is null)
        {
            return ErroresDocumentosRag.NoEncontrado;
        }

        // Reindexar una versión vieja la volvería a poner vigente junto a la nueva.
        var serie = await documentos.ObtenerSerieAsync(documento.SerieId, cancellationToken);
        if (serie.Any(otra => otra.Version > documento.Version && otra.Estado is not (EstadoIndexacion.Error or EstadoIndexacion.Retirado)))
        {
            return ErroresDocumentosRag.HayVersionMasNueva;
        }

        documento.Reindexar();
        await unitOfWork.GuardarCambiosAsync(cancellationToken);
        aviso.Avisar();
        return (await ADtosAsync([documento], cancellationToken))[0];
    }

    /// <summary>Lo saca de las consultas del asistente (ej. normativa derogada). El archivo y su historial se conservan.</summary>
    public async Task<Result<DocumentoRagDto>> RetirarAsync(Guid id, CancellationToken cancellationToken)
    {
        var administrador = await usuarioActual.ObtenerAsync(cancellationToken);
        if (!administrador.EsExitoso)
        {
            return administrador.Error!;
        }

        var documento = await documentos.ObtenerAsync(id, cancellationToken);
        if (documento is null)
        {
            return ErroresDocumentosRag.NoEncontrado;
        }

        documento.Retirar(administrador.Valor.Id);
        await unitOfWork.GuardarCambiosAsync(cancellationToken);
        return (await ADtosAsync([documento], cancellationToken))[0];
    }

    public async Task<Result<ArchivoDto>> DescargarAsync(Guid id, CancellationToken cancellationToken)
    {
        var documento = await documentos.ObtenerAsync(id, cancellationToken);
        if (documento is null)
        {
            return ErroresDocumentosRag.NoEncontrado;
        }

        var contenido = await archivos.LeerAsync(documento.StoragePath, cancellationToken);
        if (contenido is null)
        {
            return ErroresDocumentosRag.ArchivoNoEncontrado;
        }

        return new ArchivoDto(NombreArchivo(documento), "application/pdf", contenido);
    }

    /// <summary>"Reglamento académico (v2).pdf", sin caracteres que no admiten los nombres de archivo.</summary>
    private static string NombreArchivo(DocumentoRag documento)
    {
        var invalidos = Path.GetInvalidFileNameChars().Concat(['"', '/', '\\', ':', '*', '?', '<', '>', '|']).ToHashSet();
        var titulo = new string(documento.Titulo.Select(c => invalidos.Contains(c) ? '-' : c).ToArray());
        return $"{titulo} (v{documento.Version}).pdf";
    }

    private async Task<List<DocumentoRagDto>> ADtosAsync(IReadOnlyList<DocumentoRag> lista, CancellationToken cancellationToken)
    {
        var personas = await documentos.ObtenerPersonasAsync(lista.Select(documento => documento.CargadoPorUsuarioId).Distinct(), cancellationToken);
        return lista.Select(documento => new DocumentoRagDto(
                documento.Id,
                documento.SerieId,
                documento.Version,
                documento.Titulo,
                documento.Tipo,
                documento.Fuente,
                documento.FechaDocumento,
                documento.FechaCarga,
                personas.GetValueOrDefault(documento.CargadoPorUsuarioId)?.NombreParaMostrar ?? "Usuario eliminado",
                documento.Estado,
                documento.ErrorProcesamiento,
                documento.Intentos,
                documento.CantidadPaginas,
                documento.CantidadFragmentos,
                documento.FechaIndexacion))
            .ToList();
    }
}

/// <summary>
/// Procesa la cola de documentos pendientes: extrae el texto del PDF, lo divide en fragmentos, genera los embeddings
/// y los guarda en pgvector. Todo o nada: si algo falla, el documento queda en Error con un mensaje claro,
/// no se guarda ningún fragmento y la versión anterior sigue vigente.
/// </summary>
public sealed class IngestaDocumentos(
    IDocumentoRagRepository documentos,
    IFileStorage archivos,
    IExtractorTextoPdf extractor,
    IEmbeddingService embeddings,
    IUnitOfWork unitOfWork,
    IClock clock)
{
    public const string ErrorInesperado = "Error inesperado al procesar el documento. Probá reindexarlo; si se repite, revisá el registro del servidor.";

    /// <summary>Cuántos fragmentos se mandan por pedido al proveedor de embeddings.</summary>
    public const int TandaEmbeddings = 50;

    /// <summary>Al arrancar: lo que quedó "Procesando" porque el servidor se cortó vuelve a la cola.</summary>
    public async Task<int> RecuperarInterrumpidosAsync(CancellationToken cancellationToken)
    {
        var interrumpidos = await documentos.ListarProcesandoAsync(cancellationToken);
        foreach (var documento in interrumpidos)
        {
            documento.DevolverALaCola();
        }

        await unitOfWork.GuardarCambiosAsync(cancellationToken);
        return interrumpidos.Count;
    }

    /// <summary>Procesa hasta vaciar la cola. Devuelve el resultado de cada documento.</summary>
    public async Task<IReadOnlyList<ResultadoIngesta>> ProcesarPendientesAsync(CancellationToken cancellationToken)
    {
        var resultados = new List<ResultadoIngesta>();
        while (await ProcesarSiguienteAsync(cancellationToken) is { } resultado)
        {
            resultados.Add(resultado);
        }

        return resultados;
    }

    /// <summary>Procesa el pendiente más antiguo; null si no hay ninguno.</summary>
    public async Task<ResultadoIngesta?> ProcesarSiguienteAsync(CancellationToken cancellationToken)
    {
        var documento = await documentos.ObtenerSiguientePendienteAsync(cancellationToken);
        if (documento is null)
        {
            return null;
        }

        documento.IniciarProcesamiento();
        await unitOfWork.GuardarCambiosAsync(cancellationToken);

        try
        {
            var fragmentos = await IndexarAsync(documento, cancellationToken);
            return new ResultadoIngesta(documento.Id, documento.Titulo, true, fragmentos, null, null);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            var mensaje = ex switch
            {
                DomainException or PdfIlegibleException or EmbeddingNoDisponibleException => ex.Message,
                _ => ErrorInesperado,
            };

            // Se descarta todo lo que había quedado a medias y se registra el error sobre una copia limpia.
            unitOfWork.DescartarCambios();
            var limpio = await documentos.ObtenerAsync(documento.Id, CancellationToken.None);
            limpio!.MarcarError(mensaje);
            await unitOfWork.GuardarCambiosAsync(CancellationToken.None);
            return new ResultadoIngesta(documento.Id, documento.Titulo, false, 0, mensaje, ex);
        }
    }

    private async Task<int> IndexarAsync(DocumentoRag documento, CancellationToken cancellationToken)
    {
        var pdf = await archivos.LeerAsync(documento.StoragePath, cancellationToken)
            ?? throw new DomainException("No se encontró el archivo del documento. Cargalo de nuevo.");

        var paginas = extractor.Extraer(pdf);
        if (paginas.Count > DocumentoRag.PaginasMaximas)
        {
            throw new DomainException($"El PDF tiene {paginas.Count} páginas; el máximo es {DocumentoRag.PaginasMaximas}. Dividilo en partes.");
        }

        var textos = Fragmentador.Fragmentar(paginas);
        if (textos.Count == 0)
        {
            throw new DomainException("El PDF no tiene texto que se pueda leer. Si es un documento escaneado, pasalo antes por un reconocimiento de texto (OCR).");
        }

        var vectores = new List<float[]>(textos.Count);
        foreach (var tanda in textos.Chunk(TandaEmbeddings))
        {
            var generados = await embeddings.GenerarAsync(tanda.Select(texto => ParaEmbedding(documento, texto)).ToList(), PropositoEmbedding.Documento, cancellationToken);
            if (generados.Count != tanda.Length)
            {
                throw new EmbeddingNoDisponibleException("El proveedor de embeddings devolvió una cantidad de vectores distinta a la pedida.");
            }

            vectores.AddRange(generados);
        }

        var fragmentos = textos.Select((texto, indice) => new FragmentoRag(
                Guid.CreateVersion7(),
                documento.Id,
                texto.Numero,
                texto.Contenido,
                vectores[indice],
                new MetadataFragmento(documento.Titulo, documento.Tipo, documento.Version, texto.Seccion, documento.Fuente, documento.FechaDocumento),
                texto.PaginaDesde,
                texto.PaginaHasta))
            .ToList();

        // Recién ahora (todo generado sin errores) la versión nueva reemplaza a la anterior.
        foreach (var anterior in await documentos.ObtenerSerieAsync(documento.SerieId, cancellationToken))
        {
            if (anterior.Id != documento.Id && anterior.Version < documento.Version)
            {
                anterior.MarcarReemplazado();
            }
        }

        documento.MarcarIndexado(paginas.Count, fragmentos.Count, clock.UtcNow);
        await documentos.GuardarIndexacionAsync(documento.Id, fragmentos, cancellationToken);
        return fragmentos.Count;
    }

    /// <summary>El título y la sección acompañan al texto: mejoran la búsqueda sin guardarse en el contenido.</summary>
    private static string ParaEmbedding(DocumentoRag documento, FragmentoTexto texto) =>
        texto.Seccion is null
            ? $"{documento.Titulo}\n\n{texto.Contenido}"
            : $"{documento.Titulo} — {texto.Seccion}\n\n{texto.Contenido}";
}
