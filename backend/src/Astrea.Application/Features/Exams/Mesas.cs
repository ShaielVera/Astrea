using Astrea.Application.Abstractions.Clock;
using Astrea.Application.Abstractions.Persistence;
using Astrea.Application.Contracts.Pagination;
using Astrea.Application.Contracts.Results;
using Astrea.Domain.Exams;

namespace Astrea.Application.Features.Exams;

/// <summary>
/// Búsqueda de mesas. Los estudiantes ven solo mesas publicadas o canceladas (las canceladas, con su motivo,
/// para que nadie se presente) y, si no indican fechas, desde hoy en adelante. La administración ve también borradores.
/// </summary>
public sealed class ConsultasMesas(IExamRepository mesas, IClock clock)
{
    private static readonly EstadoMesa[] VisiblesParaEstudiantes = [EstadoMesa.Publicada, EstadoMesa.Cancelada];
    private static readonly EstadoMesa[] Todos = Enum.GetValues<EstadoMesa>();

    public Task<Result<PagedResult<MesaExamenDto>>> BuscarAsync(FiltroMesas filtro, int? page, int? pageSize, CancellationToken cancellationToken)
    {
        var hoy = ZonaHorariaInstitucional.Hoy(clock.UtcNow);
        if (filtro.Desde is null && filtro.Hasta is null)
        {
            filtro = filtro with { Desde = hoy };
        }

        return BuscarEnEstadosAsync(filtro, VisiblesParaEstudiantes, hoy, page, pageSize, cancellationToken);
    }

    /// <summary>Una mesa por id. Un borrador no existe para los estudiantes.</summary>
    public async Task<Result<MesaExamenDto>> ObtenerAsync(Guid mesaId, bool incluirBorradores, CancellationToken cancellationToken)
    {
        var mesa = await mesas.ObtenerDetalleAsync(mesaId, ZonaHorariaInstitucional.Hoy(clock.UtcNow), cancellationToken);
        if (mesa is null || (mesa.Estado == EstadoMesa.Borrador && !incluirBorradores))
        {
            return ErroresMesas.MesaNoEncontrada;
        }

        return mesa;
    }

    public Task<Result<PagedResult<MesaExamenDto>>> BuscarParaAdministracionAsync(FiltroMesas filtro, int? page, int? pageSize, CancellationToken cancellationToken) =>
        BuscarEnEstadosAsync(filtro, Todos, ZonaHorariaInstitucional.Hoy(clock.UtcNow), page, pageSize, cancellationToken);

    private async Task<Result<PagedResult<MesaExamenDto>>> BuscarEnEstadosAsync(
        FiltroMesas filtro,
        EstadoMesa[] permitidos,
        DateOnly hoy,
        int? page,
        int? pageSize,
        CancellationToken cancellationToken)
    {
        if (filtro.Desde > filtro.Hasta)
        {
            return ErroresMesas.RangoDeFechasInvalido;
        }

        var texto = string.IsNullOrWhiteSpace(filtro.Texto) ? null : filtro.Texto.Trim();
        if (texto?.Length > ErroresMesas.LargoMaximoTexto)
        {
            return ErroresMesas.TextoDemasiadoLargo;
        }

        var estados = filtro.Estado is { } estado ? permitidos.Where(permitido => permitido == estado).ToArray() : permitidos;
        return await mesas.BuscarAsync(filtro with { Texto = texto }, estados, hoy, PageRequest.Create(page, pageSize), cancellationToken);
    }
}

/// <summary>
/// Carga y publicación de mesas (solo Administrador). Las reglas de fechas, inscripción y estados viven en el dominio.
/// </summary>
public sealed class AdministracionMesas(IExamRepository mesas, IAcademicRepository academico, IUnitOfWork unitOfWork, IClock clock)
{
    public async Task<Result<MesaExamenDto>> CrearAsync(GuardarMesaCommand comando, CancellationToken cancellationToken)
    {
        var planMateriaId = await ResolverMateriaAsync(comando, cancellationToken);
        if (planMateriaId is null)
        {
            return ErroresMesas.MateriaNoEncontrada;
        }

        var nuevos = ADatos(comando);
        if (YaPaso(nuevos))
        {
            return ErroresMesas.FechaPasada;
        }

        var mesa = MesaExamen.Crear(Guid.CreateVersion7(), planMateriaId.Value, nuevos, clock.UtcNow);
        if (await mesas.ExisteMesaVigenteEnHorarioAsync(mesa.PlanMateriaId, mesa.Fecha, mesa.Hora, null, cancellationToken))
        {
            return ErroresMesas.HorarioOcupado;
        }

        mesas.Agregar(mesa);
        await unitOfWork.GuardarCambiosAsync(cancellationToken);
        return await DetalleAsync(mesa.Id, cancellationToken);
    }

    public async Task<Result<MesaExamenDto>> ActualizarAsync(Guid mesaId, GuardarMesaCommand comando, CancellationToken cancellationToken)
    {
        var mesa = await mesas.ObtenerAsync(mesaId, cancellationToken);
        if (mesa is null)
        {
            return ErroresMesas.MesaNoEncontrada;
        }

        var planMateriaId = await ResolverMateriaAsync(comando, cancellationToken);
        if (planMateriaId is null)
        {
            return ErroresMesas.MateriaNoEncontrada;
        }

        var datos = ADatos(comando);
        if (YaPaso(datos) && (datos.Fecha != mesa.Fecha || datos.Hora != mesa.Hora))
        {
            return ErroresMesas.FechaPasada;
        }

        if (await mesas.ExisteMesaVigenteEnHorarioAsync(planMateriaId.Value, datos.Fecha, datos.Hora, mesa.Id, cancellationToken))
        {
            return ErroresMesas.HorarioOcupado;
        }

        mesa.Actualizar(planMateriaId.Value, datos, clock.UtcNow);
        await unitOfWork.GuardarCambiosAsync(cancellationToken);
        return await DetalleAsync(mesa.Id, cancellationToken);
    }

    /// <summary>Desde Administración no se cargan mesas en una fecha y hora que ya pasaron (el sembrado histórico sí puede).</summary>
    private bool YaPaso(DatosMesa datos) => ZonaHorariaInstitucional.AInstante(datos.Fecha, datos.Hora) <= clock.UtcNow;

    public Task<Result<MesaExamenDto>> PublicarAsync(Guid mesaId, CancellationToken cancellationToken) =>
        CambiarEstadoAsync(mesaId, mesa => mesa.Publicar(clock.UtcNow), cancellationToken);

    public Task<Result<MesaExamenDto>> CancelarAsync(Guid mesaId, CancelarMesaCommand comando, CancellationToken cancellationToken) =>
        CambiarEstadoAsync(mesaId, mesa => mesa.Cancelar(comando.Motivo, clock.UtcNow), cancellationToken);

    public async Task<Result<bool>> EliminarAsync(Guid mesaId, CancellationToken cancellationToken)
    {
        var mesa = await mesas.ObtenerAsync(mesaId, cancellationToken);
        if (mesa is null)
        {
            return ErroresMesas.MesaNoEncontrada;
        }

        mesa.ValidarQueSePuedeEliminar();
        mesas.Quitar(mesa);
        await unitOfWork.GuardarCambiosAsync(cancellationToken);
        return true;
    }

    private async Task<Result<MesaExamenDto>> CambiarEstadoAsync(Guid mesaId, Action<MesaExamen> cambio, CancellationToken cancellationToken)
    {
        var mesa = await mesas.ObtenerAsync(mesaId, cancellationToken);
        if (mesa is null)
        {
            return ErroresMesas.MesaNoEncontrada;
        }

        cambio(mesa);
        await unitOfWork.GuardarCambiosAsync(cancellationToken);
        return await DetalleAsync(mesa.Id, cancellationToken);
    }

    private async Task<Result<MesaExamenDto>> DetalleAsync(Guid mesaId, CancellationToken cancellationToken) =>
        (await mesas.ObtenerDetalleAsync(mesaId, ZonaHorariaInstitucional.Hoy(clock.UtcNow), cancellationToken))!;

    /// <summary>La API identifica la materia por plan y materia; la mesa guarda la materia del plan.</summary>
    private async Task<Guid?> ResolverMateriaAsync(GuardarMesaCommand comando, CancellationToken cancellationToken)
    {
        var plan = await academico.ObtenerPlanAsync(comando.PlanId, cancellationToken);
        return plan?.Materias.SingleOrDefault(planMateria => planMateria.MateriaId == comando.MateriaId)?.Id;
    }

    private static DatosMesa ADatos(GuardarMesaCommand comando) =>
        new(
            comando.Turno,
            comando.Llamado,
            comando.Fecha,
            comando.Hora,
            comando.Aula,
            comando.Tribunal ?? [],
            comando.InscripcionDesde,
            comando.InscripcionHasta,
            comando.Observaciones);
}
