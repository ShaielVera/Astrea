using System.Data;
using System.Text.Json;
using Astrea.Application.Abstractions.Persistence;
using Astrea.Application.Contracts.Pagination;
using Astrea.Application.Features.Knowledge;
using Astrea.Application.Features.Procedures;
using Astrea.Domain.Knowledge;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Pgvector;

namespace Astrea.Infrastructure.Persistence.Repositories;

internal sealed class EfDocumentoRagRepository(AppDbContext db) : IDocumentoRagRepository
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public Task<DocumentoRag?> ObtenerAsync(Guid id, CancellationToken cancellationToken) =>
        db.DocumentosRag.SingleOrDefaultAsync(documento => documento.Id == id, cancellationToken);

    public void Agregar(DocumentoRag documento) => db.DocumentosRag.Add(documento);

    public async Task<PagedResult<DocumentoRag>> ListarAsync(FiltroDocumentosRag filtro, PageRequest pagina, CancellationToken cancellationToken)
    {
        // Una fila por documento: su versión más nueva.
        var consulta = db.DocumentosRag.AsNoTracking()
            .Where(documento => !db.DocumentosRag.Any(otra => otra.SerieId == documento.SerieId && otra.Version > documento.Version));

        if (filtro.Estados is { Count: > 0 } estados)
        {
            consulta = consulta.Where(documento => estados.Contains(documento.Estado));
        }

        if (filtro.Tipo is { } tipo)
        {
            consulta = consulta.Where(documento => documento.Tipo == tipo);
        }

        if (filtro.Texto is { } texto)
        {
            var patron = $"%{EscaparLike(texto)}%";
            consulta = consulta.Where(documento => EF.Functions.ILike(EF.Functions.Unaccent(documento.Titulo), EF.Functions.Unaccent(patron)));
        }

        var total = await consulta.CountAsync(cancellationToken);
        var items = await consulta
            .OrderByDescending(documento => documento.FechaCarga)
            .ThenBy(documento => documento.Id)
            .Skip(pagina.Skip)
            .Take(pagina.PageSize)
            .ToListAsync(cancellationToken);

        return PagedResult<DocumentoRag>.From(items, pagina, total);
    }

    public async Task<IReadOnlyList<DocumentoRag>> ObtenerSerieAsync(Guid serieId, CancellationToken cancellationToken) =>
        await db.DocumentosRag
            .Where(documento => documento.SerieId == serieId)
            .OrderByDescending(documento => documento.Version)
            .ToListAsync(cancellationToken);

    public Task<DocumentoRag?> BuscarPorHashAsync(string hashSha256, CancellationToken cancellationToken) =>
        db.DocumentosRag.AsNoTracking()
            .Where(documento => documento.HashSha256 == hashSha256 && documento.Estado != EstadoIndexacion.Retirado)
            .OrderByDescending(documento => documento.FechaCarga)
            .FirstOrDefaultAsync(cancellationToken);

    public Task<DocumentoRag?> ObtenerSiguientePendienteAsync(CancellationToken cancellationToken) =>
        db.DocumentosRag
            .Where(documento => documento.Estado == EstadoIndexacion.Pendiente)
            .OrderBy(documento => documento.FechaCarga)
            .ThenBy(documento => documento.Id)
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyList<DocumentoRag>> ListarProcesandoAsync(CancellationToken cancellationToken) =>
        await db.DocumentosRag.Where(documento => documento.Estado == EstadoIndexacion.Procesando).ToListAsync(cancellationToken);

    public async Task GuardarIndexacionAsync(Guid documentoId, IReadOnlyList<FragmentoRag> fragmentos, CancellationToken cancellationToken)
    {
        await using var transaccion = await db.Database.BeginTransactionAsync(cancellationToken);
        await db.FragmentosRag.Where(fragmento => fragmento.DocumentoRagId == documentoId).ExecuteDeleteAsync(cancellationToken);
        db.FragmentosRag.AddRange(fragmentos);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            throw new ConflictoDeConcurrenciaException(ex);
        }

        await transaccion.CommitAsync(cancellationToken);
    }

    public async Task<PagedResult<FragmentoDto>> ListarFragmentosAsync(Guid documentoId, PageRequest pagina, CancellationToken cancellationToken)
    {
        var consulta = db.FragmentosRag.AsNoTracking().Where(fragmento => fragmento.DocumentoRagId == documentoId);
        var total = await consulta.CountAsync(cancellationToken);
        var filas = await consulta
            .OrderBy(fragmento => fragmento.NumeroFragmento)
            .Skip(pagina.Skip)
            .Take(pagina.PageSize)
            .Select(fragmento => new { fragmento.NumeroFragmento, fragmento.Contenido, fragmento.Metadata, fragmento.PaginaDesde, fragmento.PaginaHasta })
            .ToListAsync(cancellationToken);

        var items = filas.Select(fila => new FragmentoDto(fila.NumeroFragmento, fila.Contenido, fila.Metadata.Seccion, fila.PaginaDesde, fila.PaginaHasta)).ToList();
        return PagedResult<FragmentoDto>.From(items, pagina, total);
    }

    public async Task<IReadOnlyList<FragmentoEncontrado>> BuscarSimilaresAsync(float[] consulta, int cantidad, CancellationToken cancellationToken)
    {
        // SQL directo: el operador <=> (distancia coseno) aprovecha el índice HNSW. Solo documentos indexados (versión vigente).
        const string sql =
            "SELECT f.documento_rag_id, f.numero_fragmento, f.contenido, f.metadata_json::text, f.pagina_desde, f.pagina_hasta, " +
            "1 - (f.embedding <=> @consulta) AS similitud " +
            "FROM fragmentos_rag f JOIN documentos_rag d ON d.id = f.documento_rag_id " +
            "WHERE d.estado = 'Indexado' " +
            "ORDER BY f.embedding <=> @consulta " +
            "LIMIT @cantidad";

        var conexion = (NpgsqlConnection)db.Database.GetDbConnection();
        var abrir = conexion.State != ConnectionState.Open;
        if (abrir)
        {
            await conexion.OpenAsync(cancellationToken);
        }

        try
        {
            await using var comando = new NpgsqlCommand(sql, conexion);
            comando.Parameters.AddWithValue("consulta", new Vector(consulta));
            comando.Parameters.AddWithValue("cantidad", cantidad);
            await using var lector = await comando.ExecuteReaderAsync(cancellationToken);
            var encontrados = new List<FragmentoEncontrado>();
            while (await lector.ReadAsync(cancellationToken))
            {
                encontrados.Add(new FragmentoEncontrado(
                    lector.GetGuid(0),
                    lector.GetInt32(1),
                    lector.GetString(2),
                    JsonSerializer.Deserialize<MetadataFragmento>(lector.GetString(3), Json)!,
                    lector.GetInt32(4),
                    lector.GetInt32(5),
                    lector.GetDouble(6)));
            }

            return encontrados;
        }
        finally
        {
            if (abrir)
            {
                await conexion.CloseAsync();
            }
        }
    }

    public async Task<IReadOnlyDictionary<Guid, PersonaDto>> ObtenerPersonasAsync(IEnumerable<Guid> usuarioIds, CancellationToken cancellationToken)
    {
        var ids = usuarioIds.ToList();
        return await db.Usuarios.AsNoTracking()
            .Where(usuario => ids.Contains(usuario.Id))
            .Select(usuario => new PersonaDto(usuario.Id, usuario.Email, usuario.Nombre, usuario.Apellido, usuario.Legajo, null))
            .ToDictionaryAsync(persona => persona.Id, cancellationToken);
    }

    private static string EscaparLike(string texto) =>
        texto.Replace(@"\", @"\\", StringComparison.Ordinal)
            .Replace("%", @"\%", StringComparison.Ordinal)
            .Replace("_", @"\_", StringComparison.Ordinal);
}
