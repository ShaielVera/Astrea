using Astrea.Application.Abstractions.Clock;
using Astrea.Application.Abstractions.Persistence;
using Astrea.Application.Contracts.Pagination;
using Astrea.Application.Contracts.Results;
using Astrea.Application.Features.Exams;
using Astrea.Domain.Academic;
using Astrea.Domain.Exams;
using AwesomeAssertions;
using NSubstitute;

namespace Astrea.Application.UnitTests.Features.Exams;

public class MesasTests
{
    // 14/11/2026 22:30 en Argentina: en UTC ya es 15/11.
    private static readonly DateTimeOffset Ahora = new(2026, 11, 15, 1, 30, 0, TimeSpan.Zero);
    private static readonly DateOnly HoyEnElInstituto = new(2026, 11, 14);

    private readonly IExamRepository _mesas = Substitute.For<IExamRepository>();
    private readonly IAcademicRepository _academico = Substitute.For<IAcademicRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly IClock _clock = Substitute.For<IClock>();
    private readonly PlanEstudio _plan;
    private readonly Materia _materia;

    public MesasTests()
    {
        _clock.UtcNow.Returns(Ahora);
        var carrera = Carrera.Crear(Guid.NewGuid(), "TSDS", "Desarrollo de Software", 3, Ahora);
        _plan = PlanEstudio.Crear(Guid.NewGuid(), carrera, "2120/16", "Plan", null, null);
        _materia = Materia.Crear(Guid.NewGuid(), "TSDS-001", "Programación I");
        _plan.AgregarMateria(Guid.NewGuid(), _materia, 1, Cuatrimestre.Anual, null, true);
        _academico.ObtenerPlanAsync(_plan.Id, Arg.Any<CancellationToken>()).Returns(_plan);
        _mesas.BuscarAsync(default!, default!, default, default!, default)
            .ReturnsForAnyArgs(new PagedResult<MesaExamenDto>([], 1, 20, 0));
    }

    private ConsultasMesas Consultas() => new(_mesas, _clock);

    private AdministracionMesas Administracion() => new(_mesas, _academico, _unitOfWork, _clock);

    private GuardarMesaCommand Comando(Guid? materiaId = null) =>
        new(_plan.Id, materiaId ?? _materia.Id, "Diciembre 2026", 1, new DateOnly(2026, 12, 10), new TimeOnly(18, 0), "Aula 3", ["Prof. Gómez"], null, null, null);

    [Fact]
    public async Task Sin_fechas_el_estudiante_ve_desde_hoy_en_el_instituto_y_solo_mesas_visibles()
    {
        await Consultas().BuscarAsync(new FiltroMesas(), null, null, CancellationToken.None);

        await _mesas.Received(1).BuscarAsync(
            Arg.Is<FiltroMesas>(filtro => filtro.Desde == HoyEnElInstituto && filtro.Hasta == null),
            Arg.Is<IReadOnlyCollection<EstadoMesa>>(estados => estados.Count == 2 && !estados.Contains(EstadoMesa.Borrador)),
            HoyEnElInstituto,
            Arg.Any<PageRequest>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Un_estudiante_que_pide_borradores_no_recibe_ninguno()
    {
        await Consultas().BuscarAsync(new FiltroMesas(Estado: EstadoMesa.Borrador), null, null, CancellationToken.None);

        await _mesas.Received(1).BuscarAsync(
            Arg.Any<FiltroMesas>(),
            Arg.Is<IReadOnlyCollection<EstadoMesa>>(estados => estados.Count == 0),
            Arg.Any<DateOnly>(),
            Arg.Any<PageRequest>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task La_administracion_ve_todos_los_estados_y_no_recibe_fecha_por_defecto()
    {
        await Consultas().BuscarParaAdministracionAsync(new FiltroMesas(Texto: "  gómez  "), 2, 500, CancellationToken.None);

        await _mesas.Received(1).BuscarAsync(
            Arg.Is<FiltroMesas>(filtro => filtro.Desde == null && filtro.Texto == "gómez"),
            Arg.Is<IReadOnlyCollection<EstadoMesa>>(estados => estados.Count == 3),
            Arg.Any<DateOnly>(),
            Arg.Is<PageRequest>(pagina => pagina.Page == 2 && pagina.PageSize == PageRequest.MaxPageSize),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Un_rango_invertido_o_un_texto_muy_largo_es_un_error_de_validacion()
    {
        var rango = await Consultas().BuscarAsync(new FiltroMesas(new DateOnly(2026, 12, 10), new DateOnly(2026, 12, 1)), null, null, CancellationToken.None);
        var texto = await Consultas().BuscarAsync(new FiltroMesas(Texto: new string('a', 101)), null, null, CancellationToken.None);

        rango.Error.Should().Be(ErroresMesas.RangoDeFechasInvalido);
        texto.Error.Should().Be(ErroresMesas.TextoDemasiadoLargo);
        await _mesas.DidNotReceiveWithAnyArgs().BuscarAsync(default!, default!, default, default!, default);
    }

    [Fact]
    public async Task Un_borrador_no_existe_para_los_estudiantes()
    {
        var id = Guid.NewGuid();
        _mesas.ObtenerDetalleAsync(id, Arg.Any<DateOnly>(), Arg.Any<CancellationToken>()).Returns(Detalle(id, EstadoMesa.Borrador));

        (await Consultas().ObtenerAsync(id, incluirBorradores: false, CancellationToken.None)).Error.Should().Be(ErroresMesas.MesaNoEncontrada);
        (await Consultas().ObtenerAsync(id, incluirBorradores: true, CancellationToken.None)).EsExitoso.Should().BeTrue();
    }

    [Fact]
    public async Task Crear_una_mesa_la_guarda_como_borrador_de_la_materia_del_plan()
    {
        _mesas.ObtenerDetalleAsync(Arg.Any<Guid>(), Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns(call => Detalle(call.Arg<Guid>(), EstadoMesa.Borrador));

        var resultado = await Administracion().CrearAsync(Comando(), CancellationToken.None);

        resultado.EsExitoso.Should().BeTrue();
        _mesas.Received(1).Agregar(Arg.Is<MesaExamen>(mesa =>
            mesa.PlanMateriaId == _plan.Materias.Single().Id && mesa.Estado == EstadoMesa.Borrador));
        await _unitOfWork.Received(1).GuardarCambiosAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Una_materia_de_otro_plan_no_se_encuentra()
    {
        var resultado = await Administracion().CrearAsync(Comando(Guid.NewGuid()), CancellationToken.None);

        resultado.Error.Should().Be(ErroresMesas.MateriaNoEncontrada);
        _mesas.DidNotReceiveWithAnyArgs().Agregar(default!);
    }

    [Fact]
    public async Task Otra_mesa_vigente_en_el_mismo_horario_es_un_conflicto()
    {
        _mesas.ExisteMesaVigenteEnHorarioAsync(_plan.Materias.Single().Id, new DateOnly(2026, 12, 10), new TimeOnly(18, 0), null, Arg.Any<CancellationToken>())
            .Returns(true);

        var resultado = await Administracion().CrearAsync(Comando(), CancellationToken.None);

        resultado.Error!.Tipo.Should().Be(TipoError.Conflicto);
        await _unitOfWork.DidNotReceive().GuardarCambiosAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Publicar_una_mesa_inexistente_devuelve_no_encontrada()
    {
        var resultado = await Administracion().PublicarAsync(Guid.NewGuid(), CancellationToken.None);

        resultado.Error.Should().Be(ErroresMesas.MesaNoEncontrada);
    }

    private MesaExamenDto Detalle(Guid id, EstadoMesa estado) =>
        new(
            id,
            estado,
            "Diciembre 2026",
            1,
            new DateOnly(2026, 12, 10),
            new TimeOnly(18, 0),
            ZonaHorariaInstitucional.AInstante(new DateOnly(2026, 12, 10), new TimeOnly(18, 0)),
            null,
            ["Prof. Gómez"],
            null,
            null,
            EstadoInscripcion.SinFechas,
            null,
            null,
            _plan.Id,
            _plan.CarreraId,
            "TSDS",
            "Desarrollo de Software",
            _materia.Id,
            _materia.Codigo,
            _materia.Nombre,
            1);
}
