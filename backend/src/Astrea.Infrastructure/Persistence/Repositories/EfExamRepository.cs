using Astrea.Application.Abstractions.Persistence;
using Astrea.Application.Contracts.Pagination;
using Astrea.Application.Features.Exams;
using Astrea.Domain.Exams;
using Microsoft.EntityFrameworkCore;

namespace Astrea.Infrastructure.Persistence.Repositories;

internal sealed class EfExamRepository(AppDbContext db) : IExamRepository
{
    public Task<MesaExamen?> ObtenerAsync(Guid mesaId, CancellationToken cancellationToken) =>
        db.MesasExamen.SingleOrDefaultAsync(mesa => mesa.Id == mesaId, cancellationToken);

    public void Agregar(MesaExamen mesa) => db.MesasExamen.Add(mesa);

    public void Quitar(MesaExamen mesa) => db.MesasExamen.Remove(mesa);

    public Task<bool> ExisteMesaVigenteEnHorarioAsync(Guid planMateriaId, DateOnly fecha, TimeOnly hora, Guid? excluirMesaId, CancellationToken cancellationToken) =>
        db.MesasExamen.AnyAsync(
            mesa => mesa.PlanMateriaId == planMateriaId
                && mesa.Fecha == fecha
                && mesa.Hora == hora
                && mesa.Estado != EstadoMesa.Cancelada
                && mesa.Id != excluirMesaId,
            cancellationToken);

    public async Task<PagedResult<MesaExamenDto>> BuscarAsync(
        FiltroMesas filtro,
        IReadOnlyCollection<EstadoMesa> estados,
        DateOnly hoy,
        PageRequest pagina,
        CancellationToken cancellationToken)
    {
        var consulta = Filas().Where(fila => estados.Contains(fila.Mesa.Estado));

        if (filtro.Desde is { } desde)
        {
            consulta = consulta.Where(fila => fila.Mesa.Fecha >= desde);
        }

        if (filtro.Hasta is { } hasta)
        {
            consulta = consulta.Where(fila => fila.Mesa.Fecha <= hasta);
        }

        if (filtro.CarreraId is { } carreraId)
        {
            consulta = consulta.Where(fila => fila.CarreraId == carreraId);
        }

        if (filtro.MateriaId is { } materiaId)
        {
            consulta = consulta.Where(fila => fila.MateriaId == materiaId);
        }

        if (filtro.Texto is { } texto)
        {
            // Sin distinguir tildes ni mayúsculas: "programacion" encuentra "Programación".
            var patron = $"%{EscaparLike(texto)}%";
            consulta = consulta.Where(fila =>
                EF.Functions.ILike(EF.Functions.Unaccent(fila.MateriaNombre), EF.Functions.Unaccent(patron))
                || fila.Mesa.Tribunal.Any(integrante => EF.Functions.ILike(EF.Functions.Unaccent(integrante), EF.Functions.Unaccent(patron))));
        }

        var total = await consulta.CountAsync(cancellationToken);
        var filas = await consulta
            .OrderBy(fila => fila.Mesa.Fecha)
            .ThenBy(fila => fila.Mesa.Hora)
            .ThenBy(fila => fila.MateriaNombre)
            .ThenBy(fila => fila.Mesa.Id)
            .Skip(pagina.Skip)
            .Take(pagina.PageSize)
            .ToListAsync(cancellationToken);

        return PagedResult<MesaExamenDto>.From(filas.Select(fila => ADto(fila, hoy)).ToList(), pagina, total);
    }

    public async Task<MesaExamenDto?> ObtenerDetalleAsync(Guid mesaId, DateOnly hoy, CancellationToken cancellationToken)
    {
        var fila = await Filas().SingleOrDefaultAsync(candidata => candidata.Mesa.Id == mesaId, cancellationToken);
        return fila is null ? null : ADto(fila, hoy);
    }

    /// <summary>La mesa con los datos de su materia, plan y carrera, para mostrar y filtrar.</summary>
    private IQueryable<FilaMesa> Filas() =>
        from mesa in db.MesasExamen.AsNoTracking()
        join planMateria in db.PlanMaterias.AsNoTracking() on mesa.PlanMateriaId equals planMateria.Id
        join materia in db.Materias.AsNoTracking() on planMateria.MateriaId equals materia.Id
        join plan in db.PlanesEstudio.AsNoTracking() on planMateria.PlanEstudioId equals plan.Id
        join carrera in db.Carreras.AsNoTracking() on plan.CarreraId equals carrera.Id
        select new FilaMesa
        {
            Mesa = mesa,
            PlanId = plan.Id,
            CarreraId = carrera.Id,
            CarreraCodigo = carrera.Codigo,
            CarreraNombre = carrera.Nombre,
            MateriaId = materia.Id,
            MateriaCodigo = materia.Codigo,
            MateriaNombre = materia.Nombre,
            AnioMateria = planMateria.AnioEnPlan,
        };

    private static MesaExamenDto ADto(FilaMesa fila, DateOnly hoy)
    {
        var mesa = fila.Mesa;
        return new MesaExamenDto(
            mesa.Id,
            mesa.Estado,
            mesa.Turno,
            mesa.Llamado,
            mesa.Fecha,
            mesa.Hora,
            mesa.Inicio,
            mesa.Aula,
            mesa.Tribunal,
            mesa.InscripcionDesde,
            mesa.InscripcionHasta,
            mesa.EstadoInscripcionAl(hoy),
            mesa.Observaciones,
            mesa.MotivoCancelacion,
            fila.PlanId,
            fila.CarreraId,
            fila.CarreraCodigo,
            fila.CarreraNombre,
            fila.MateriaId,
            fila.MateriaCodigo,
            fila.MateriaNombre,
            fila.AnioMateria);
    }

    private static string EscaparLike(string texto) =>
        texto.Replace(@"\", @"\\", StringComparison.Ordinal)
            .Replace("%", @"\%", StringComparison.Ordinal)
            .Replace("_", @"\_", StringComparison.Ordinal);

    private sealed class FilaMesa
    {
        public required MesaExamen Mesa { get; init; }

        public Guid PlanId { get; init; }

        public Guid CarreraId { get; init; }

        public required string CarreraCodigo { get; init; }

        public required string CarreraNombre { get; init; }

        public Guid MateriaId { get; init; }

        public required string MateriaCodigo { get; init; }

        public required string MateriaNombre { get; init; }

        public int AnioMateria { get; init; }
    }
}
