using Astrea.Application.Abstractions.Clock;
using Astrea.Application.Abstractions.Persistence;
using Astrea.Application.Contracts.Results;
using Astrea.Application.Features.Academic;
using Astrea.Domain.Academic;
using AwesomeAssertions;
using NSubstitute;

namespace Astrea.Application.UnitTests.Features.Academic;

public class AdministracionCatalogoTests
{
    private static readonly DateTimeOffset Ahora = new(2026, 11, 14, 12, 0, 0, TimeSpan.Zero);

    private readonly IAcademicRepository _academico = Substitute.For<IAcademicRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly IClock _clock = Substitute.For<IClock>();

    public AdministracionCatalogoTests()
    {
        _clock.UtcNow.Returns(Ahora);
    }

    private AdministracionCatalogo Crear() => new(_academico, _unitOfWork, _clock);

    [Fact]
    public async Task Crear_una_carrera_la_guarda_con_el_codigo_normalizado()
    {
        var resultado = await Crear().CrearCarreraAsync(new GuardarCarreraCommand("t.s.d.s.", "Desarrollo de Software", 3), CancellationToken.None);

        resultado.Valor.Codigo.Should().Be("TSDS");
        _academico.Received(1).Agregar(Arg.Is<Carrera>(c => c.Codigo == "TSDS"));
        await _unitOfWork.Received(1).GuardarCambiosAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Un_codigo_de_carrera_repetido_devuelve_conflicto_y_no_guarda()
    {
        _academico.ExisteCarreraConCodigoAsync("TSDS", Arg.Any<CancellationToken>()).Returns(true);

        var resultado = await Crear().CrearCarreraAsync(new GuardarCarreraCommand("TSDS", "Otra", 3), CancellationToken.None);

        resultado.Error!.Tipo.Should().Be(TipoError.Conflicto);
        _academico.DidNotReceive().Agregar(Arg.Any<Carrera>());
        await _unitOfWork.DidNotReceive().GuardarCambiosAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Agregar_una_materia_genera_un_codigo_libre_a_partir_de_la_carrera()
    {
        var carrera = Carrera.Crear(Guid.NewGuid(), "TSDS", "Desarrollo de Software", 3, Ahora);
        var plan = PlanEstudio.Crear(Guid.NewGuid(), carrera, "Res. 2120/16", "Plan", null, null);
        _academico.ObtenerPlanAsync(plan.Id, Arg.Any<CancellationToken>()).Returns(plan);
        _academico.ObtenerCarreraAsync(carrera.Id, Arg.Any<CancellationToken>()).Returns(carrera);
        _academico.ExisteMateriaConCodigoAsync("TSDS-001", Arg.Any<CancellationToken>()).Returns(true);

        var resultado = await Crear().AgregarMateriaAsync(
            plan.Id,
            new AgregarMateriaCommand("Programación I", 2, Cuatrimestre.Anual, 6),
            CancellationToken.None);

        resultado.Valor.Codigo.Should().Be("TSDS-002");
        resultado.Valor.Anio.Should().Be(2);
        plan.Materias.Should().ContainSingle();
        _academico.Received(1).Agregar(Arg.Is<Materia>(m => m.Nombre == "Programación I"));
    }

    [Fact]
    public async Task Agregar_una_materia_a_un_plan_inexistente_devuelve_no_encontrado()
    {
        var resultado = await Crear().AgregarMateriaAsync(Guid.NewGuid(), new AgregarMateriaCommand("X", 1, Cuatrimestre.Anual, null), CancellationToken.None);

        resultado.Error.Should().Be(ErroresAcademicos.PlanNoEncontrado);
    }

    [Fact]
    public async Task Actualizar_una_carrera_inexistente_devuelve_no_encontrado()
    {
        var resultado = await Crear().ActualizarCarreraAsync(Guid.NewGuid(), new GuardarCarreraCommand("X", "X", 3), CancellationToken.None);

        resultado.Error.Should().Be(ErroresAcademicos.CarreraNoEncontrada);
    }
}
