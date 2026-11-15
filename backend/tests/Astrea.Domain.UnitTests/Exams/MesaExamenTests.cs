using Astrea.Domain.Common;
using Astrea.Domain.Exams;
using AwesomeAssertions;

namespace Astrea.Domain.UnitTests.Exams;

public class MesaExamenTests
{
    // 14/11/2026 12:00 en Argentina (15:00 UTC).
    private static readonly DateTimeOffset Ahora = new(2026, 11, 14, 15, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly DiaExamen = new(2026, 12, 10);

    private static DatosMesa Datos(
        DateOnly? fecha = null,
        TimeOnly? hora = null,
        DateOnly? inscripcionDesde = null,
        DateOnly? inscripcionHasta = null,
        IReadOnlyList<string>? tribunal = null,
        int llamado = 1) =>
        new(
            "Diciembre 2026",
            llamado,
            fecha ?? DiaExamen,
            hora ?? new TimeOnly(18, 0),
            "Aula 3",
            tribunal ?? ["Prof. Ana Gómez", "Prof. Luis Pérez"],
            inscripcionDesde,
            inscripcionHasta,
            null);

    private static MesaExamen Crear(DatosMesa? datos = null) => MesaExamen.Crear(Guid.NewGuid(), Guid.NewGuid(), datos ?? Datos(), Ahora);

    [Fact]
    public void Una_mesa_nueva_es_un_borrador_con_sus_datos_normalizados()
    {
        var mesa = Crear(Datos(hora: new TimeOnly(18, 0, 45), tribunal: ["  Prof. Ana Gómez ", "", "Prof. Luis Pérez"]));

        mesa.Estado.Should().Be(EstadoMesa.Borrador);
        mesa.Hora.Should().Be(new TimeOnly(18, 0));
        mesa.Tribunal.Should().Equal("Prof. Ana Gómez", "Prof. Luis Pérez");
        mesa.PublicadaAt.Should().BeNull();
    }

    [Fact]
    public void El_inicio_se_expresa_en_hora_argentina()
    {
        var mesa = Crear(Datos(hora: new TimeOnly(23, 30)));

        mesa.Inicio.Offset.Should().Be(TimeSpan.FromHours(-3));
        mesa.Inicio.UtcDateTime.Should().Be(new DateTime(2026, 12, 11, 2, 30, 0, DateTimeKind.Utc));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(6)]
    public void El_llamado_debe_estar_entre_1_y_5(int llamado)
    {
        var crear = () => Crear(Datos(llamado: llamado));

        crear.Should().Throw<DomainException>().WithMessage("*llamado*");
    }

    [Fact]
    public void El_tribunal_necesita_al_menos_un_integrante_y_sin_repetidos()
    {
        ((Action)(() => Crear(Datos(tribunal: [" "])))).Should().Throw<DomainException>().WithMessage("*al menos un integrante*");
        ((Action)(() => Crear(Datos(tribunal: ["Prof. Gómez", "prof. gómez"])))).Should().Throw<DomainException>().WithMessage("*dos veces*");
        ((Action)(() => Crear(Datos(tribunal: ["A", "B", "C", "D", "E", "F"])))).Should().Throw<DomainException>().WithMessage("*más de 5*");
    }

    [Fact]
    public void La_inscripcion_requiere_ambas_fechas_o_ninguna()
    {
        var crear = () => Crear(Datos(inscripcionDesde: new DateOnly(2026, 12, 1)));

        crear.Should().Throw<DomainException>().WithMessage("*inicio y el cierre*");
    }

    [Fact]
    public void La_inscripcion_no_puede_cerrar_antes_de_abrir()
    {
        var crear = () => Crear(Datos(inscripcionDesde: new DateOnly(2026, 12, 5), inscripcionHasta: new DateOnly(2026, 12, 1)));

        crear.Should().Throw<DomainException>().WithMessage("*cerrar antes de abrir*");
    }

    [Fact]
    public void La_inscripcion_cierra_antes_del_dia_del_examen()
    {
        var crear = () => Crear(Datos(inscripcionDesde: new DateOnly(2026, 12, 1), inscripcionHasta: DiaExamen));

        crear.Should().Throw<DomainException>().WithMessage("*antes del día del examen*");
    }

    [Theory]
    [InlineData("2026-11-30", EstadoInscripcion.Proxima)]
    [InlineData("2026-12-01", EstadoInscripcion.Abierta)]
    [InlineData("2026-12-07", EstadoInscripcion.Abierta)]
    [InlineData("2026-12-08", EstadoInscripcion.Cerrada)]
    public void El_estado_de_la_inscripcion_depende_del_dia(string hoy, EstadoInscripcion esperado)
    {
        var mesa = Crear(Datos(inscripcionDesde: new DateOnly(2026, 12, 1), inscripcionHasta: new DateOnly(2026, 12, 7)));

        mesa.EstadoInscripcionAl(DateOnly.Parse(hoy, System.Globalization.CultureInfo.InvariantCulture)).Should().Be(esperado);
    }

    [Fact]
    public void Sin_fechas_de_inscripcion_no_hay_estado_de_inscripcion()
    {
        Crear().EstadoInscripcionAl(new DateOnly(2026, 12, 1)).Should().Be(EstadoInscripcion.SinFechas);
    }

    [Fact]
    public void Publicar_un_borrador_registra_la_fecha_de_publicacion()
    {
        var mesa = Crear();

        mesa.Publicar(Ahora);

        mesa.Estado.Should().Be(EstadoMesa.Publicada);
        mesa.PublicadaAt.Should().Be(Ahora);
    }

    [Fact]
    public void No_se_publica_una_mesa_cuya_fecha_ya_paso()
    {
        var mesa = Crear(Datos(fecha: new DateOnly(2026, 11, 14), hora: new TimeOnly(11, 0)));

        var publicar = () => mesa.Publicar(Ahora);

        publicar.Should().Throw<DomainException>().WithMessage("*ya pasó*");
        mesa.Estado.Should().Be(EstadoMesa.Borrador);
    }

    [Fact]
    public void No_se_publica_dos_veces()
    {
        var mesa = Crear();
        mesa.Publicar(Ahora);

        var publicar = () => mesa.Publicar(Ahora);

        publicar.Should().Throw<DomainException>().WithMessage("*ya está publicada*");
    }

    [Fact]
    public void Cancelar_una_mesa_publicada_exige_motivo_y_cierra_la_inscripcion()
    {
        var mesa = Crear(Datos(inscripcionDesde: new DateOnly(2026, 12, 1), inscripcionHasta: new DateOnly(2026, 12, 7)));
        mesa.Publicar(Ahora);

        ((Action)(() => mesa.Cancelar(" ", Ahora))).Should().Throw<DomainException>().WithMessage("*motivo*");
        mesa.Cancelar("Licencia del presidente del tribunal", Ahora);

        mesa.Estado.Should().Be(EstadoMesa.Cancelada);
        mesa.MotivoCancelacion.Should().Be("Licencia del presidente del tribunal");
        mesa.CanceladaAt.Should().Be(Ahora);
        mesa.EstadoInscripcionAl(new DateOnly(2026, 12, 3)).Should().Be(EstadoInscripcion.Cerrada);
    }

    [Fact]
    public void Un_borrador_no_se_cancela_y_una_cancelada_no_se_modifica_ni_se_publica()
    {
        var borrador = Crear();
        ((Action)(() => borrador.Cancelar("x", Ahora))).Should().Throw<DomainException>().WithMessage("*eliminalo*");

        var cancelada = Crear();
        cancelada.Publicar(Ahora);
        cancelada.Cancelar("Paro docente", Ahora);

        ((Action)(() => cancelada.Actualizar(cancelada.PlanMateriaId, Datos(), Ahora))).Should().Throw<DomainException>().WithMessage("*cancelada*");
        ((Action)(() => cancelada.Publicar(Ahora))).Should().Throw<DomainException>().WithMessage("*cancelada*");
    }

    [Fact]
    public void Solo_se_eliminan_borradores()
    {
        var mesa = Crear();
        mesa.ValidarQueSePuedeEliminar();
        mesa.Publicar(Ahora);

        var eliminar = mesa.ValidarQueSePuedeEliminar;

        eliminar.Should().Throw<DomainException>().WithMessage("*se cancela*");
    }

    [Fact]
    public void Una_mesa_publicada_no_puede_moverse_a_una_fecha_pasada()
    {
        var mesa = Crear();
        mesa.Publicar(Ahora);

        var actualizar = () => mesa.Actualizar(mesa.PlanMateriaId, Datos(fecha: new DateOnly(2026, 11, 1)), Ahora);

        actualizar.Should().Throw<DomainException>().WithMessage("*ya pasó*");
    }

    [Theory]
    [InlineData(23, 59, 2026, 11, 14)] // 23:59 UTC = 20:59 en Argentina: todavía es 14.
    [InlineData(2, 59, 2026, 11, 13)]  // 02:59 UTC = 23:59 del día anterior en Argentina.
    [InlineData(3, 0, 2026, 11, 14)]
    public void El_dia_de_hoy_es_el_del_instituto_y_no_el_de_UTC(int horaUtc, int minutoUtc, int anio, int mes, int dia)
    {
        var ahora = new DateTimeOffset(2026, 11, 14, horaUtc, minutoUtc, 0, TimeSpan.Zero);

        ZonaHorariaInstitucional.Hoy(ahora).Should().Be(new DateOnly(anio, mes, dia));
    }
}
