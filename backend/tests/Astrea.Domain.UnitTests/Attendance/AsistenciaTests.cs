using Astrea.Domain.Attendance;
using Astrea.Domain.Common;
using Astrea.Domain.Users;
using AwesomeAssertions;

namespace Astrea.Domain.UnitTests.Attendance;

public class AsistenciaTests
{
    // 15:00 UTC = 12:00 en el instituto.
    private static readonly DateTimeOffset Ahora = new(2027, 6, 1, 15, 0, 0, TimeSpan.Zero);

    private static RelevamientoAsistencia Relevamiento(DateOnly? cierre = null) =>
        RelevamientoAsistencia.Abrir(Guid.NewGuid(), "Antes del receso de julio", cierre ?? new DateOnly(2027, 6, 30), Guid.NewGuid(), Ahora);

    [Fact]
    public void Un_relevamiento_queda_abierto_hasta_el_dia_de_cierre_inclusive()
    {
        var relevamiento = Relevamiento(new DateOnly(2027, 6, 30));

        relevamiento.CicloLectivo.Should().Be(2027);
        relevamiento.EstaAbierto(Ahora).Should().BeTrue();
        // 30/6 a las 23:00 en Argentina ya es 1/7 en UTC: sigue abierto.
        relevamiento.EstaAbierto(new DateTimeOffset(2027, 7, 1, 2, 0, 0, TimeSpan.Zero)).Should().BeTrue();
        relevamiento.EstaAbierto(new DateTimeOffset(2027, 7, 1, 4, 0, 0, TimeSpan.Zero)).Should().BeFalse();
    }

    [Theory]
    [InlineData("2027-05-31")]
    [InlineData("2027-10-30")]
    public void La_fecha_de_cierre_no_puede_haber_pasado_ni_estar_a_mas_de_120_dias(string cierre) =>
        FluentActions.Invoking(() => Relevamiento(DateOnly.Parse(cierre))).Should().Throw<DomainException>();

    [Fact]
    public void El_titulo_es_obligatorio() =>
        FluentActions.Invoking(() => RelevamientoAsistencia.Abrir(Guid.NewGuid(), "  ", new DateOnly(2027, 6, 30), Guid.NewGuid(), Ahora))
            .Should().Throw<DomainException>();

    [Fact]
    public void Cerrado_antes_de_tiempo_ya_no_acepta_confirmaciones_ni_cambios()
    {
        var relevamiento = Relevamiento();
        var confirmacion = ConfirmacionAsistencia.Crear(Guid.NewGuid(), relevamiento, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), ValorAsistencia.AsisteRegularmente, null, Ahora);

        relevamiento.Cerrar(Ahora.AddDays(1));

        relevamiento.EstaAbierto(Ahora.AddDays(2)).Should().BeFalse();
        FluentActions.Invoking(() => confirmacion.Actualizar(relevamiento, Guid.NewGuid(), ValorAsistencia.NoAsiste, null, Ahora.AddDays(2)))
            .Should().Throw<DomainException>().WithMessage("*cerrado*");
        FluentActions.Invoking(() => ConfirmacionAsistencia.Crear(Guid.NewGuid(), relevamiento, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), ValorAsistencia.NoAsiste, null, Ahora.AddDays(2)))
            .Should().Throw<DomainException>();
        FluentActions.Invoking(() => relevamiento.Cerrar(Ahora.AddDays(3))).Should().Throw<DomainException>();
    }

    [Fact]
    public void Corregir_una_confirmacion_registra_quien_y_cuando()
    {
        var relevamiento = Relevamiento();
        var primerDocente = Guid.NewGuid();
        var segundoDocente = Guid.NewGuid();
        var confirmacion = ConfirmacionAsistencia.Crear(Guid.NewGuid(), relevamiento, Guid.NewGuid(), Guid.NewGuid(), primerDocente, ValorAsistencia.AsisteRegularmente, null, Ahora);

        confirmacion.Actualizar(relevamiento, segundoDocente, ValorAsistencia.NoAsiste, "  No vino desde abril  ", Ahora.AddHours(3));

        confirmacion.Should().BeEquivalentTo(new
        {
            Valor = ValorAsistencia.NoAsiste,
            Observacion = "No vino desde abril",
            DocenteId = segundoDocente,
            ConfirmadaEn = Ahora.AddHours(3),
        });
    }

    [Fact]
    public void Rechaza_valores_desconocidos_y_observaciones_largas()
    {
        var relevamiento = Relevamiento();

        FluentActions.Invoking(() => ConfirmacionAsistencia.Crear(Guid.NewGuid(), relevamiento, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), (ValorAsistencia)9, null, Ahora))
            .Should().Throw<DomainException>();
        FluentActions.Invoking(() => ConfirmacionAsistencia.Crear(Guid.NewGuid(), relevamiento, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), ValorAsistencia.NoAsiste, new string('x', 201), Ahora))
            .Should().Throw<DomainException>();
    }

    [Fact]
    public void Una_confirmacion_no_se_puede_mover_a_otro_relevamiento()
    {
        var confirmacion = ConfirmacionAsistencia.Crear(Guid.NewGuid(), Relevamiento(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), ValorAsistencia.AsisteRegularmente, null, Ahora);

        FluentActions.Invoking(() => confirmacion.Actualizar(Relevamiento(), Guid.NewGuid(), ValorAsistencia.NoAsiste, null, Ahora))
            .Should().Throw<DomainException>();
    }

    [Fact]
    public void La_inscripcion_es_del_ciclo_lectivo_del_instituto()
    {
        // 1/1 a las 01:00 UTC todavía es 31/12 en Argentina.
        var inscripcion = InscripcionCursada.Crear(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), new DateTimeOffset(2027, 1, 1, 1, 0, 0, TimeSpan.Zero));

        inscripcion.CicloLectivo.Should().Be(2026);
    }

    [Fact]
    public void El_rol_de_docente_se_puede_quitar_pero_el_de_estudiante_no()
    {
        var usuario = Usuario.Registrar(Guid.NewGuid(), "uid", "profe@astrea.test", Ahora);
        usuario.AsignarRol(Rol.Docente, Guid.NewGuid(), Ahora);
        usuario.CodigosDeRol().Should().Contain(Rol.CodigoDocente);

        usuario.QuitarRol(Rol.Docente, Ahora);

        usuario.TieneRol(Rol.Docente).Should().BeFalse();
        FluentActions.Invoking(() => usuario.QuitarRol(Rol.Estudiante, Ahora)).Should().Throw<DomainException>();
    }

    [Fact]
    public void Se_muestra_con_su_nombre_oficial_o_su_email()
    {
        var usuario = Usuario.Registrar(Guid.NewGuid(), "uid", "Ana@Astrea.test", Ahora);
        usuario.NombreParaMostrar.Should().Be("ana@astrea.test");

        usuario.ActualizarDatosOficiales("Ana", "Pérez", "30.123.456", null, Ahora);
        usuario.NombreParaMostrar.Should().Be("Pérez, Ana");
    }
}
