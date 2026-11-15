using Astrea.Domain.Common;
using Astrea.Domain.Procedures;
using AwesomeAssertions;

namespace Astrea.Domain.UnitTests.Procedures;

public class TramiteTests
{
    private static readonly DateTimeOffset Ahora = new(2026, 11, 15, 12, 0, 0, TimeSpan.Zero);
    private static readonly Guid Estudiante = Guid.NewGuid();
    private static readonly Guid Secretaria = Guid.NewGuid();
    private static readonly string CarreraId = Guid.NewGuid().ToString();

    private static TramiteTipo AlumnoRegular() => CatalogoTramites.Tipos().Single(tipo => tipo.Id == CatalogoTramites.AlumnoRegularId);

    private static TramiteTipo ConstanciaExamen() => CatalogoTramites.Tipos().Single(tipo => tipo.Id == CatalogoTramites.ConstanciaExamenId);

    private static Tramite Solicitar(Dictionary<string, string?>? datos = null) =>
        Tramite.Solicitar(Guid.NewGuid(), Estudiante, AlumnoRegular(), datos ?? new Dictionary<string, string?> { ["carrera"] = CarreraId, ["presentarAnte"] = "ANSES" }, null, Ahora);

    [Fact]
    public void Un_tramite_nuevo_queda_pendiente_con_su_evento_de_creacion()
    {
        var tramite = Solicitar();

        tramite.Estado.Should().Be(EstadoTramite.Pendiente);
        tramite.EsTitular(Estudiante).Should().BeTrue();
        tramite.Eventos.Should().ContainSingle().Which.Should().Match<TramiteEvento>(evento =>
            evento.EstadoAnterior == null && evento.EstadoNuevo == EstadoTramite.Pendiente && evento.ActorUsuarioId == Estudiante);
    }

    [Fact]
    public void Los_datos_se_validan_y_normalizan_contra_el_formulario_del_tipo()
    {
        var tramite = Tramite.Solicitar(
            Guid.NewGuid(),
            Estudiante,
            ConstanciaExamen(),
            new Dictionary<string, string?> { ["carrera"] = CarreraId, ["materia"] = "  Programación II ", ["fechaExamen"] = "2026-12-10", ["presentarAnte"] = " " },
            null,
            Ahora);

        tramite.Datos.Should().BeEquivalentTo(new Dictionary<string, string>
        {
            ["carrera"] = CarreraId,
            ["materia"] = "Programación II",
            ["fechaExamen"] = "2026-12-10",
        });
    }

    [Theory]
    [InlineData("presentarAnte", null, "Completá el campo")]
    [InlineData("carrera", "no-es-un-id", "Elegí una carrera")]
    [InlineData("extra", "x", "no tiene el campo")]
    public void Datos_invalidos_se_rechazan_con_un_mensaje_claro(string clave, string? valor, string mensaje)
    {
        var datos = new Dictionary<string, string?> { ["carrera"] = CarreraId, ["presentarAnte"] = "ANSES" };
        datos[clave] = valor;

        var solicitar = () => Solicitar(datos);

        solicitar.Should().Throw<DomainException>().WithMessage($"*{mensaje}*");
    }

    [Fact]
    public void Una_fecha_mal_escrita_se_rechaza()
    {
        var solicitar = () => Tramite.Solicitar(
            Guid.NewGuid(),
            Estudiante,
            ConstanciaExamen(),
            new Dictionary<string, string?> { ["carrera"] = CarreraId, ["materia"] = "Física", ["fechaExamen"] = "10/12/2026" },
            null,
            Ahora);

        solicitar.Should().Throw<DomainException>().WithMessage("*no es una fecha válida*");
    }

    [Fact]
    public void Un_tipo_inactivo_no_se_puede_pedir()
    {
        var tipo = AlumnoRegular();
        tipo.CambiarActivo(false);

        var solicitar = () => Tramite.Solicitar(Guid.NewGuid(), Estudiante, tipo, new Dictionary<string, string?> { ["carrera"] = CarreraId, ["presentarAnte"] = "x" }, null, Ahora);

        solicitar.Should().Throw<DomainException>().WithMessage("*no se puede pedir*");
    }

    [Fact]
    public void Tomar_lo_pasa_a_en_proceso_asignado_al_administrador()
    {
        var tramite = Solicitar();

        tramite.Tomar(Secretaria, Ahora.AddHours(1));

        tramite.Estado.Should().Be(EstadoTramite.EnProceso);
        tramite.AdministradorAsignadoId.Should().Be(Secretaria);
        tramite.Eventos.Last().Should().Match<TramiteEvento>(evento =>
            evento.EstadoAnterior == EstadoTramite.Pendiente && evento.EstadoNuevo == EstadoTramite.EnProceso && evento.ActorUsuarioId == Secretaria);
    }

    [Fact]
    public void Aprobar_lo_deja_listo_con_el_comentario_para_el_estudiante()
    {
        var tramite = Solicitar();
        tramite.Tomar(Secretaria, Ahora);

        tramite.Aprobar(Secretaria, "Retirala en Secretaría de 18 a 21", Ahora);

        tramite.Estado.Should().Be(EstadoTramite.Listo);
        tramite.ObservacionAdministrativa.Should().Be("Retirala en Secretaría de 18 a 21");
        tramite.Eventos.Select(evento => evento.EstadoNuevo).Should().Equal(EstadoTramite.Pendiente, EstadoTramite.EnProceso, EstadoTramite.Listo);
    }

    [Fact]
    public void Se_puede_aprobar_o_rechazar_directo_desde_pendiente()
    {
        var aprobado = Solicitar();
        var rechazado = Solicitar();

        aprobado.Aprobar(Secretaria, null, Ahora);
        rechazado.Rechazar(Secretaria, "Faltan materias aprobadas", Ahora);

        aprobado.Estado.Should().Be(EstadoTramite.Listo);
        aprobado.AdministradorAsignadoId.Should().Be(Secretaria);
        rechazado.Estado.Should().Be(EstadoTramite.Rechazado);
    }

    [Fact]
    public void Rechazar_exige_motivo()
    {
        var tramite = Solicitar();

        var rechazar = () => tramite.Rechazar(Secretaria, "  ", Ahora);

        rechazar.Should().Throw<DomainException>().WithMessage("*motivo*");
        tramite.Estado.Should().Be(EstadoTramite.Pendiente);
        tramite.Eventos.Should().ContainSingle();
    }

    [Fact]
    public void Un_tramite_finalizado_no_cambia_mas_de_estado()
    {
        var tramite = Solicitar();
        tramite.Rechazar(Secretaria, "Datos incorrectos", Ahora);

        ((Action)(() => tramite.Aprobar(Secretaria, null, Ahora))).Should().Throw<DomainException>().WithMessage("*finalizado*");
        ((Action)(() => tramite.Rechazar(Secretaria, "otra vez", Ahora))).Should().Throw<DomainException>().WithMessage("*finalizado*");
        ((Action)(() => tramite.Tomar(Secretaria, Ahora))).Should().Throw<DomainException>().WithMessage("*finalizado*");
        tramite.Eventos.Should().HaveCount(2);
    }

    [Fact]
    public void No_se_toma_dos_veces()
    {
        var tramite = Solicitar();
        tramite.Tomar(Secretaria, Ahora);

        var tomar = () => tramite.Tomar(Guid.NewGuid(), Ahora);

        tomar.Should().Throw<DomainException>().WithMessage("*ya está en proceso*");
        tramite.AdministradorAsignadoId.Should().Be(Secretaria);
    }

    [Fact]
    public void Todo_cambio_de_estado_registra_al_actor()
    {
        var tramite = Solicitar();

        var aprobar = () => tramite.Aprobar(Guid.Empty, null, Ahora);

        aprobar.Should().Throw<DomainException>().WithMessage("*quién lo hizo*");
    }

    [Fact]
    public void El_catalogo_inicial_tiene_los_cuatro_tramites_del_SDD_y_todos_piden_carrera()
    {
        var tipos = CatalogoTramites.Tipos();

        tipos.Select(tipo => tipo.Codigo).Should().BeEquivalentTo("ALUMNO_REGULAR", "CONSTANCIA_EXAMEN", "ANALITICO_PARCIAL", "JUSTIFICACION_INASISTENCIA");
        tipos.Should().OnlyContain(tipo => tipo.Campos.Any(campo => campo.Tipo == TipoCampo.Carrera && campo.Obligatorio));
    }
}
