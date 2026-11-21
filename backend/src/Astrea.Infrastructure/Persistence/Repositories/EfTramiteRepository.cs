using Astrea.Application.Abstractions.Persistence;
using Astrea.Application.Contracts.Pagination;
using Astrea.Application.Features.Procedures;
using Astrea.Domain.Procedures;
using Microsoft.EntityFrameworkCore;

namespace Astrea.Infrastructure.Persistence.Repositories;

internal sealed class EfTramiteRepository(AppDbContext db) : ITramiteRepository
{
    public async Task<IReadOnlyList<TramiteTipo>> ListarTiposAsync(bool incluirInactivos, CancellationToken cancellationToken) =>
        await db.TramiteTipos.AsNoTracking()
            .Where(tipo => incluirInactivos || tipo.Activo)
            .OrderBy(tipo => tipo.Nombre)
            .ThenBy(tipo => tipo.Id)
            .ToListAsync(cancellationToken);

    public Task<TramiteTipo?> ObtenerTipoAsync(Guid tipoId, CancellationToken cancellationToken) =>
        db.TramiteTipos.SingleOrDefaultAsync(tipo => tipo.Id == tipoId, cancellationToken);

    public Task<Tramite?> ObtenerAsync(Guid tramiteId, CancellationToken cancellationToken) =>
        db.Tramites.Include(tramite => tramite.Eventos).SingleOrDefaultAsync(tramite => tramite.Id == tramiteId, cancellationToken);

    public Task<Tramite?> ObtenerPorClaveIdempotenciaAsync(Guid usuarioId, Guid clave, CancellationToken cancellationToken) =>
        db.Tramites.Include(tramite => tramite.Eventos)
            .SingleOrDefaultAsync(tramite => tramite.UsuarioId == usuarioId && tramite.ClaveIdempotencia == clave, cancellationToken);

    public void Agregar(Tramite tramite) => db.Tramites.Add(tramite);

    public async Task<PagedResult<TramiteResumenDto>> BuscarAsync(FiltroTramites filtro, PageRequest pagina, CancellationToken cancellationToken)
    {
        var consulta =
            from tramite in db.Tramites.AsNoTracking()
            join tipo in db.TramiteTipos.AsNoTracking() on tramite.TramiteTipoId equals tipo.Id
            join usuario in db.Usuarios.AsNoTracking() on tramite.UsuarioId equals usuario.Id
            select new { tramite, tipo, usuario };

        if (filtro.UsuarioId is { } usuarioId)
        {
            consulta = consulta.Where(fila => fila.tramite.UsuarioId == usuarioId);
        }

        if (filtro.Estados is { Count: > 0 } estados)
        {
            consulta = consulta.Where(fila => estados.Contains(fila.tramite.Estado));
        }

        if (filtro.TipoId is { } tipoId)
        {
            consulta = consulta.Where(fila => fila.tramite.TramiteTipoId == tipoId);
        }

        if (filtro.Texto is { } texto)
        {
            // "T-000123", "123" → por número; cualquier otro texto → email, apellido o nombre del titular, sin tildes.
            var digitos = texto.TrimStart('T', 't', '-', ' ');
            if (int.TryParse(digitos, out var numero))
            {
                consulta = consulta.Where(fila => fila.tramite.Numero == numero);
            }
            else
            {
                var patron = $"%{EscaparLike(texto)}%";
                consulta = consulta.Where(fila =>
                    EF.Functions.ILike(fila.usuario.Email, patron)
                    || EF.Functions.ILike(EF.Functions.Unaccent(fila.usuario.Apellido ?? string.Empty), EF.Functions.Unaccent(patron))
                    || EF.Functions.ILike(EF.Functions.Unaccent(fila.usuario.Nombre ?? string.Empty), EF.Functions.Unaccent(patron)));
            }
        }

        var total = await consulta.CountAsync(cancellationToken);
        var ordenada = filtro.MasRecientesPrimero
            ? consulta.OrderByDescending(fila => fila.tramite.FechaSolicitud).ThenByDescending(fila => fila.tramite.Id)
            : consulta.OrderBy(fila => fila.tramite.FechaSolicitud).ThenBy(fila => fila.tramite.Id);

        var filas = await ordenada
            .Skip(pagina.Skip)
            .Take(pagina.PageSize)
            .Select(fila => new
            {
                fila.tramite.Id,
                fila.tramite.Numero,
                TipoId = fila.tipo.Id,
                TipoNombre = fila.tipo.Nombre,
                fila.tramite.Estado,
                fila.tramite.FechaSolicitud,
                fila.tramite.UpdatedAt,
                fila.usuario.Email,
                fila.usuario.Apellido,
                fila.usuario.Nombre,
            })
            .ToListAsync(cancellationToken);

        var items = filas
            .Select(fila => new TramiteResumenDto(
                fila.Id,
                NumeroTramite.Formatear(fila.Numero),
                fila.TipoId,
                fila.TipoNombre,
                fila.Estado,
                fila.FechaSolicitud,
                fila.UpdatedAt,
                fila.Email,
                fila.Apellido,
                fila.Nombre))
            .ToList();

        return PagedResult<TramiteResumenDto>.From(items, pagina, total);
    }

    public async Task<IReadOnlyDictionary<Guid, PersonaDto>> ObtenerPersonasAsync(IEnumerable<Guid> usuarioIds, CancellationToken cancellationToken)
    {
        var ids = usuarioIds.ToList();
        return await db.Usuarios.AsNoTracking()
            .Where(usuario => ids.Contains(usuario.Id))
            .Select(usuario => new PersonaDto(
                usuario.Id,
                usuario.Email,
                usuario.Nombre,
                usuario.Apellido,
                usuario.Legajo,
                usuario.Dni,
                usuario.EmailContacto,
                usuario.EmailContactoVerificadoEn,
                usuario.Celular,
                usuario.CelularVerificadoEn))
            .ToDictionaryAsync(persona => persona.Id, cancellationToken);
    }

    public Task<PlantillaDocumento?> ObtenerPlantillaVigenteAsync(Guid tipoId, CancellationToken cancellationToken) =>
        db.PlantillasDocumento
            .Where(plantilla => plantilla.TramiteTipoId == tipoId)
            .OrderByDescending(plantilla => plantilla.Version)
            .FirstOrDefaultAsync(cancellationToken);

    public void Agregar(PlantillaDocumento plantilla) => db.PlantillasDocumento.Add(plantilla);

    public void Agregar(DocumentoEmitido documento) => db.DocumentosEmitidos.Add(documento);

    public Task<DocumentoEmitido?> ObtenerDocumentoDeTramiteAsync(Guid tramiteId, CancellationToken cancellationToken) =>
        db.DocumentosEmitidos
            .Where(documento => documento.TramiteId == tramiteId)
            .OrderByDescending(documento => documento.FechaEmision)
            .ThenByDescending(documento => documento.Id)
            .FirstOrDefaultAsync(cancellationToken);

    public Task<DocumentoEmitido?> ObtenerDocumentoPorCodigoAsync(string codigoValidacion, CancellationToken cancellationToken) =>
        db.DocumentosEmitidos.SingleOrDefaultAsync(documento => documento.CodigoValidacion == codigoValidacion, cancellationToken);

    public Task<bool> ExisteCodigoValidacionAsync(string codigoValidacion, CancellationToken cancellationToken) =>
        db.DocumentosEmitidos.AnyAsync(documento => documento.CodigoValidacion == codigoValidacion, cancellationToken);

    private static string EscaparLike(string texto) =>
        texto.Replace(@"\", @"\\", StringComparison.Ordinal)
            .Replace("%", @"\%", StringComparison.Ordinal)
            .Replace("_", @"\_", StringComparison.Ordinal);
}
