using Astrea.Application.Abstractions.Persistence;
using Astrea.Application.Contracts.Pagination;
using Astrea.Application.Features.LostAndFound;
using Astrea.Domain.LostAndFound;
using Microsoft.EntityFrameworkCore;

namespace Astrea.Infrastructure.Persistence.Repositories;

internal sealed class EfObjetoPerdidoRepository(AppDbContext db) : IObjetoPerdidoRepository
{
    public Task<ObjetoPerdido?> ObtenerAsync(Guid id, CancellationToken cancellationToken) =>
        db.ObjetosPerdidos.Include(objeto => objeto.Entrega).SingleOrDefaultAsync(objeto => objeto.Id == id, cancellationToken);

    public void Agregar(ObjetoPerdido objeto) => db.ObjetosPerdidos.Add(objeto);

    public async Task<PagedResult<ObjetoPerdido>> BuscarAsync(FiltroHallazgos filtro, PageRequest pagina, CancellationToken cancellationToken)
    {
        var estados = filtro.Estados;
        var consulta = db.ObjetosPerdidos.AsNoTracking().Include(objeto => objeto.Entrega).Where(objeto => estados.Contains(objeto.Estado));

        if (filtro.SoloConFoto)
        {
            consulta = consulta.Where(objeto => objeto.FotoStoragePath != null);
        }

        if (filtro.Categoria is { } categoria)
        {
            consulta = consulta.Where(objeto => objeto.Categoria == categoria);
        }

        if (filtro.Punto is { } punto)
        {
            consulta = consulta.Where(objeto => objeto.PuntoResguardo == punto);
        }

        if (filtro.PublicadoPor is { } publicador)
        {
            consulta = consulta.Where(objeto => objeto.PublicadoPorUsuarioId == publicador);
        }

        if (filtro.Texto is { } texto)
        {
            // Solo campos públicos: buscar en la descripción privada permitiría adivinar los detalles de verificación.
            var patron = $"%{EscaparLike(texto)}%";
            consulta = consulta.Where(objeto =>
                EF.Functions.ILike(EF.Functions.Unaccent(objeto.TituloPublico), EF.Functions.Unaccent(patron))
                || EF.Functions.ILike(EF.Functions.Unaccent(objeto.SectorEncontrado), EF.Functions.Unaccent(patron)));
        }

        var total = await consulta.CountAsync(cancellationToken);
        var items = await consulta
            .OrderByDescending(objeto => objeto.FechaHallazgo)
            .ThenByDescending(objeto => objeto.FechaPublicacion)
            .ThenBy(objeto => objeto.Id)
            .Skip(pagina.Skip)
            .Take(pagina.PageSize)
            .ToListAsync(cancellationToken);

        return PagedResult<ObjetoPerdido>.From(items, pagina, total);
    }

    public async Task<IReadOnlyList<ObjetoPerdido>> ListarCerradosAntesDeAsync(DateTimeOffset limite, int maximo, CancellationToken cancellationToken) =>
        await db.ObjetosPerdidos.Include(objeto => objeto.Entrega)
            .Where(objeto => objeto.Estado != EstadoObjeto.Publicado && objeto.FechaCierre != null && objeto.FechaCierre <= limite)
            .OrderBy(objeto => objeto.FechaCierre)
            .Take(maximo)
            .ToListAsync(cancellationToken);

    public void Eliminar(ObjetoPerdido objeto)
    {
        if (objeto.Entrega is { } entrega)
        {
            db.EntregasObjeto.Remove(entrega);
        }

        db.ObjetosPerdidos.Remove(objeto);
    }

    public async Task<IReadOnlyDictionary<Guid, PersonaHallazgoDto>> ObtenerPersonasAsync(IEnumerable<Guid> usuarioIds, CancellationToken cancellationToken)
    {
        var ids = usuarioIds.ToList();
        return await db.Usuarios.AsNoTracking()
            .Where(usuario => ids.Contains(usuario.Id))
            .Select(usuario => new PersonaHallazgoDto(usuario.Id, usuario.Email, usuario.Nombre, usuario.Apellido))
            .ToDictionaryAsync(persona => persona.Id, cancellationToken);
    }

    private static string EscaparLike(string texto) =>
        texto.Replace(@"\", @"\\", StringComparison.Ordinal)
            .Replace("%", @"\%", StringComparison.Ordinal)
            .Replace("_", @"\_", StringComparison.Ordinal);
}
