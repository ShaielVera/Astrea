using Astrea.Application.Contracts.Pagination;
using Astrea.Application.Features.Exams;
using Astrea.Domain.Exams;

namespace Astrea.Application.Abstractions.Persistence;

public interface IExamRepository
{
    Task<MesaExamen?> ObtenerAsync(Guid mesaId, CancellationToken cancellationToken);

    void Agregar(MesaExamen mesa);

    void Quitar(MesaExamen mesa);

    /// <summary>¿Hay otra mesa no cancelada de la misma materia del plan en el mismo día y hora?</summary>
    Task<bool> ExisteMesaVigenteEnHorarioAsync(Guid planMateriaId, DateOnly fecha, TimeOnly hora, Guid? excluirMesaId, CancellationToken cancellationToken);

    /// <summary>
    /// Mesas filtradas y paginadas, ordenadas por fecha, hora, materia e id (orden estable).
    /// Con <paramref name="estados"/> se limita la búsqueda a esos estados; <paramref name="hoy"/> define el estado de la inscripción.
    /// </summary>
    Task<PagedResult<MesaExamenDto>> BuscarAsync(
        FiltroMesas filtro,
        IReadOnlyCollection<EstadoMesa> estados,
        DateOnly hoy,
        PageRequest pagina,
        CancellationToken cancellationToken);

    Task<MesaExamenDto?> ObtenerDetalleAsync(Guid mesaId, DateOnly hoy, CancellationToken cancellationToken);
}
