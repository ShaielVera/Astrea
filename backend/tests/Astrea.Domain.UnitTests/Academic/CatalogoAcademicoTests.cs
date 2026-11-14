using Astrea.Domain.Academic;
using Astrea.Domain.Common;
using AwesomeAssertions;

namespace Astrea.Domain.UnitTests.Academic;

public class CarreraTests
{
    private static readonly DateTimeOffset Ahora = new(2026, 11, 14, 12, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData("TSDS", "TSDS")]
    [InlineData("t.s.d.s.", "TSDS")]
    [InlineData(" tssiti ", "TSSITI")]
    public void El_codigo_se_normaliza_sin_puntos_y_en_mayusculas(string codigo, string esperado)
    {
        Carrera.Crear(Guid.NewGuid(), codigo, "Tecnicatura", 3, Ahora).Codigo.Should().Be(esperado);
    }

    [Theory]
    [InlineData("")]
    [InlineData("X")]
    [InlineData("CON ESPACIO")]
    [InlineData("CODIGO-DEMASIADO-LARGO-PARA-SER-VALIDO")]
    public void Un_codigo_invalido_se_rechaza(string codigo)
    {
        var crear = () => Carrera.Crear(Guid.NewGuid(), codigo, "Tecnicatura", 3, Ahora);

        crear.Should().Throw<DomainException>();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(7)]
    public void La_duracion_debe_estar_entre_1_y_6_anios(int duracion)
    {
        var crear = () => Carrera.Crear(Guid.NewGuid(), "TSDS", "Tecnicatura", duracion, Ahora);

        crear.Should().Throw<DomainException>();
    }

    [Fact]
    public void El_nombre_es_obligatorio()
    {
        var crear = () => Carrera.Crear(Guid.NewGuid(), "TSDS", "   ", 3, Ahora);

        crear.Should().Throw<DomainException>().WithMessage("*nombre*");
    }

    [Fact]
    public void Actualizar_cambia_nombre_duracion_y_estado()
    {
        var carrera = Carrera.Crear(Guid.NewGuid(), "TSDS", "Tecnicatura", 3, Ahora);

        carrera.Actualizar("Tecnicatura Superior en Desarrollo de Software", 4, activa: false, Ahora.AddDays(1));

        carrera.Nombre.Should().Be("Tecnicatura Superior en Desarrollo de Software");
        carrera.DuracionAnios.Should().Be(4);
        carrera.Activa.Should().BeFalse();
        carrera.UpdatedAt.Should().Be(Ahora.AddDays(1));
    }
}

public class PlanEstudioTests
{
    private static readonly DateTimeOffset Ahora = new(2026, 11, 14, 12, 0, 0, TimeSpan.Zero);

    private static PlanEstudio NuevoPlan(int duracion = 3) =>
        PlanEstudio.Crear(Guid.NewGuid(), Carrera.Crear(Guid.NewGuid(), "TSDS", "Tecnicatura", duracion, Ahora), "Res. 2120/16", "Plan 2016", null, null);

    private static Materia NuevaMateria(string codigo = "TSDS-001") => Materia.Crear(Guid.NewGuid(), codigo, "Programación I");

    [Fact]
    public void Las_materias_se_ordenan_dentro_de_cada_anio()
    {
        var plan = NuevoPlan();

        var primera = plan.AgregarMateria(Guid.NewGuid(), NuevaMateria("A-1"), 1, Cuatrimestre.Anual, null, true);
        var segunda = plan.AgregarMateria(Guid.NewGuid(), NuevaMateria("A-2"), 1, Cuatrimestre.Primero, null, true);
        var deSegundoAnio = plan.AgregarMateria(Guid.NewGuid(), NuevaMateria("A-3"), 2, Cuatrimestre.Anual, null, true);

        primera.Orden.Should().Be(1);
        segunda.Orden.Should().Be(2);
        deSegundoAnio.Orden.Should().Be(1);
        plan.Materias.Should().HaveCount(3);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(4)]
    public void El_anio_de_una_materia_no_puede_superar_la_duracion_de_la_carrera(int anio)
    {
        var plan = NuevoPlan(duracion: 3);

        var agregar = () => plan.AgregarMateria(Guid.NewGuid(), NuevaMateria(), anio, Cuatrimestre.Anual, null, true);

        agregar.Should().Throw<DomainException>().WithMessage("*entre 1 y 3*");
    }

    [Fact]
    public void Una_materia_no_puede_estar_dos_veces_en_el_mismo_plan()
    {
        var plan = NuevoPlan();
        var materia = NuevaMateria();
        plan.AgregarMateria(Guid.NewGuid(), materia, 1, Cuatrimestre.Anual, null, true);

        var repetir = () => plan.AgregarMateria(Guid.NewGuid(), materia, 2, Cuatrimestre.Anual, null, true);

        repetir.Should().Throw<DomainException>();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(41)]
    public void La_carga_horaria_debe_ser_razonable(int carga)
    {
        var agregar = () => NuevoPlan().AgregarMateria(Guid.NewGuid(), NuevaMateria(), 1, Cuatrimestre.Anual, carga, true);

        agregar.Should().Throw<DomainException>();
    }

    [Fact]
    public void Actualizar_una_materia_cambia_su_ubicacion_en_el_plan()
    {
        var plan = NuevoPlan();
        var planMateria = plan.AgregarMateria(Guid.NewGuid(), NuevaMateria(), 1, Cuatrimestre.Anual, null, true);

        plan.ActualizarMateria(planMateria.Id, 2, Cuatrimestre.Segundo, 4, obligatoria: false);

        planMateria.AnioEnPlan.Should().Be(2);
        planMateria.Cuatrimestre.Should().Be(Cuatrimestre.Segundo);
        planMateria.CargaHorariaSemanal.Should().Be(4);
        planMateria.Obligatoria.Should().BeFalse();
    }

    [Fact]
    public void La_vigencia_no_puede_terminar_antes_de_empezar()
    {
        var plan = NuevoPlan();

        var actualizar = () => plan.Actualizar("Res. 1", "Plan", new DateOnly(2020, 1, 1), new DateOnly(2019, 1, 1), true);

        actualizar.Should().Throw<DomainException>();
    }
}
