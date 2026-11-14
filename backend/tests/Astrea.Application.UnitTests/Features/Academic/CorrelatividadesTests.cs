using Astrea.Application.Abstractions.Persistence;
using Astrea.Application.Contracts.Results;
using Astrea.Application.Features.Academic;
using Astrea.Domain.Academic;
using AwesomeAssertions;
using NSubstitute;

namespace Astrea.Application.UnitTests.Features.Academic;

public class CorrelatividadesTests
{
    private static readonly DateTimeOffset Ahora = new(2026, 11, 14, 12, 0, 0, TimeSpan.Zero);

    private readonly IAcademicRepository _academico = Substitute.For<IAcademicRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly PlanEstudio _plan;
    private readonly Materia _programacionI = Materia.Crear(Guid.NewGuid(), "TSDS-001", "Programación I");
    private readonly Materia _programacionII = Materia.Crear(Guid.NewGuid(), "TSDS-002", "Programación II");
    private readonly PlanMateria _pmProgramacionI;
    private readonly PlanMateria _pmProgramacionII;

    public CorrelatividadesTests()
    {
        var carrera = Carrera.Crear(Guid.NewGuid(), "TSDS", "Desarrollo de Software", 3, Ahora);
        _plan = PlanEstudio.Crear(Guid.NewGuid(), carrera, "2120/16", "Plan", null, null);
        _pmProgramacionI = _plan.AgregarMateria(Guid.NewGuid(), _programacionI, 2, Cuatrimestre.Anual, null, true);
        _pmProgramacionII = _plan.AgregarMateria(Guid.NewGuid(), _programacionII, 3, Cuatrimestre.Anual, null, true);
        _academico.ObtenerPlanAsync(_plan.Id, Arg.Any<CancellationToken>()).Returns(_plan);
    }

    private Correlatividades Crear() => new(_academico, _unitOfWork);

    [Fact]
    public async Task Una_materia_inexistente_devuelve_no_encontrado()
    {
        var resultado = await Crear().ObtenerAsync(_plan.Id, Guid.NewGuid(), incluirInactivas: false, CancellationToken.None);

        resultado.Error!.Tipo.Should().Be(TipoError.NoEncontrado);
    }

    [Fact]
    public async Task Agregar_traduce_las_materias_del_plan_y_guarda()
    {
        var detalle = Detalle([]);
        _academico.ObtenerCorrelatividadesAsync(_plan.Id, _programacionII.Id, true, Arg.Any<CancellationToken>()).Returns(detalle);

        var resultado = await Crear().AgregarAsync(
            _plan.Id,
            new AgregarCorrelatividadCommand(_programacionII.Id, TipoCorrelatividad.Rendir, _programacionI.Id, CondicionRequisito.Aprobada, null, null),
            CancellationToken.None);

        resultado.Valor.Should().BeSameAs(detalle);
        _plan.Correlatividades.Should().ContainSingle().Which.Should().Match<Correlatividad>(c =>
            c.PlanMateriaDestinoId == _pmProgramacionII.Id && c.PlanMateriaRequisitoId == _pmProgramacionI.Id);
        await _unitOfWork.Received(1).GuardarCambiosAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Agregar_un_requisito_de_otro_plan_devuelve_no_encontrado_y_no_guarda()
    {
        var resultado = await Crear().AgregarAsync(
            _plan.Id,
            new AgregarCorrelatividadCommand(_programacionII.Id, TipoCorrelatividad.Rendir, Guid.NewGuid(), CondicionRequisito.Aprobada, null, null),
            CancellationToken.None);

        resultado.Error!.Tipo.Should().Be(TipoError.NoEncontrado);
        await _unitOfWork.DidNotReceive().GuardarCambiosAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Quitar_una_correlatividad_ajena_devuelve_no_encontrado()
    {
        var resultado = await Crear().QuitarAsync(_plan.Id, Guid.NewGuid(), CancellationToken.None);

        resultado.Error!.Tipo.Should().Be(TipoError.NoEncontrado);
    }

    [Theory]
    [InlineData(EstadoMateria.Regularizada, ResultadoVerificacion.NoCumple)]
    [InlineData(EstadoMateria.Aprobada, ResultadoVerificacion.Cumple)]
    public async Task Verificar_usa_los_estados_declarados_por_materia(EstadoMateria estado, ResultadoVerificacion esperado)
    {
        var regla = _plan.AgregarCorrelatividad(Guid.NewGuid(), _pmProgramacionII.Id, TipoCorrelatividad.Rendir, _pmProgramacionI.Id, CondicionRequisito.Aprobada, null, null);
        var requisito = new RequisitoDto(regla.Id, CondicionRequisito.Aprobada, null, _programacionI.Id, "TSDS-001", "Programación I", 2, null);
        _academico.ObtenerCorrelatividadesAsync(_plan.Id, _programacionII.Id, false, Arg.Any<CancellationToken>()).Returns(Detalle(rendir: [requisito]));

        var resultado = await Crear().VerificarAsync(
            _plan.Id,
            _programacionII.Id,
            new VerificarCorrelatividadesCommand(TipoCorrelatividad.Rendir, [new EstadoDeMateria(_programacionI.Id, estado), new EstadoDeMateria(Guid.NewGuid(), EstadoMateria.Aprobada)]),
            CancellationToken.None);

        resultado.Valor.Resultado.Should().Be(esperado);
        resultado.Valor.Faltantes.Should().HaveCount(esperado == ResultadoVerificacion.Cumple ? 0 : 1);
    }

    [Fact]
    public async Task Verificar_con_un_tipo_invalido_es_error_de_validacion()
    {
        var resultado = await Crear().VerificarAsync(_plan.Id, _programacionII.Id, new VerificarCorrelatividadesCommand((TipoCorrelatividad)9, null), CancellationToken.None);

        resultado.Error!.Tipo.Should().Be(TipoError.Validacion);
    }

    private CorrelatividadesDeMateriaDto Detalle(IReadOnlyList<RequisitoDto> rendir) =>
        new(_plan.Id, _pmProgramacionII.Id, _programacionII.Id, _programacionII.Codigo, _programacionII.Nombre, 3, [], rendir);
}
