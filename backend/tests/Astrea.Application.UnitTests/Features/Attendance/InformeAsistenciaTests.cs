using Astrea.Application.Abstractions.Clock;
using Astrea.Application.Abstractions.Identity;
using Astrea.Application.Abstractions.Persistence;
using Astrea.Application.Contracts.Results;
using Astrea.Application.Features.Attendance;
using Astrea.Application.Features.Auth;
using Astrea.Domain.Attendance;
using AwesomeAssertions;
using NSubstitute;

namespace Astrea.Application.UnitTests.Features.Attendance;

public class InformeAsistenciaTests
{
    private static readonly DateTimeOffset Ahora = new(2027, 6, 1, 15, 0, 0, TimeSpan.Zero);

    private readonly IAsistenciaRepository _asistencia = Substitute.For<IAsistenciaRepository>();
    private readonly RelevamientoAsistencia _relevamiento = RelevamientoAsistencia.Abrir(Guid.NewGuid(), "Receso de julio", new DateOnly(2027, 6, 30), Guid.NewGuid(), Ahora);

    private static readonly MateriaCursadaDto Didactica = new(Guid.NewGuid(), "Didáctica", 1, Guid.NewGuid(), "Profesorado");
    private static readonly MateriaCursadaDto Pedagogia = new(Guid.NewGuid(), "Pedagogía", 1, Didactica.CarreraId, "Profesorado");

    private readonly Guid _regular = Guid.NewGuid();
    private readonly Guid _ausente = Guid.NewGuid();
    private readonly Guid _sinDni = Guid.NewGuid();

    public InformeAsistenciaTests()
    {
        _asistencia.ObtenerRelevamientoAsync(_relevamiento.Id, Arg.Any<CancellationToken>()).Returns(_relevamiento);
        _asistencia.DatosDelInformeAsync(_relevamiento, Arg.Any<CancellationToken>()).Returns(new DatosInforme(
            [
                new FilaInscripcion(_regular, "Álvarez, Ana", "ana@astrea.test", "30111222", Didactica),
                new FilaInscripcion(_ausente, "Zapata, Juan", "juan@astrea.test", "40111222", Didactica),
                new FilaInscripcion(_ausente, "Zapata, Juan", "juan@astrea.test", "40111222", Pedagogia),
                new FilaInscripcion(_sinDni, "sin-datos@astrea.test", "sin-datos@astrea.test", null, Pedagogia),
            ],
            [
                new FilaConfirmacion(_regular, Didactica.PlanMateriaId, ValorAsistencia.AsisteRegularmente, null, "Prof. Gómez", Ahora),
                new FilaConfirmacion(_ausente, Didactica.PlanMateriaId, ValorAsistencia.NoAsiste, "No vino desde abril", "Prof. Gómez", Ahora),
            ],
            new Dictionary<Guid, IReadOnlyList<string>> { [Didactica.PlanMateriaId] = ["Prof. Gómez"] }));
    }

    private RelevamientosAsistencia Servicio()
    {
        var clock = Substitute.For<IClock>();
        clock.UtcNow.Returns(Ahora);
        return new RelevamientosAsistencia(new UsuarioActual(Substitute.For<ICurrentUser>(), Substitute.For<IUserRepository>()), _asistencia, Substitute.For<IUnitOfWork>(), clock);
    }

    [Fact]
    public async Task Ordena_primero_a_quienes_no_asisten_y_cuenta_lo_que_falta_confirmar()
    {
        var informe = (await Servicio().InformeAsync(_relevamiento.Id, null, null, null, null, CancellationToken.None)).Valor;

        informe.Estudiantes.Items.Select(estudiante => estudiante.Id).Should().Equal(_ausente, _regular, _sinDni);
        informe.Estudiantes.Items[0].Should().BeEquivalentTo(new { NoAsiste = 1, Regular = 0, SinConfirmar = 1 });
        informe.Estudiantes.Items[0].Materias.Should().ContainSingle(materia => materia.Valor == ValorAsistencia.NoAsiste)
            .Which.Should().BeEquivalentTo(new { Materia = "Didáctica", Observacion = "No vino desde abril", Docente = "Prof. Gómez" });
    }

    [Fact]
    public async Task Filtra_por_dni_aunque_vengan_con_puntos_y_avisa_los_que_no_encontro()
    {
        var informe = (await Servicio().InformeAsync(_relevamiento.Id, null, "30.111.222; 12.345.678\n30111222", null, null, CancellationToken.None)).Valor;

        informe.Estudiantes.Items.Should().ContainSingle().Which.Id.Should().Be(_regular);
        informe.DnisSinDatos.Should().Equal("12345678");
    }

    [Fact]
    public async Task Busca_por_nombre_email_o_dni()
    {
        (await Servicio().InformeAsync(_relevamiento.Id, "zapata", null, null, null, CancellationToken.None)).Valor.Estudiantes.Items.Should().ContainSingle().Which.Id.Should().Be(_ausente);
        (await Servicio().InformeAsync(_relevamiento.Id, "sin-datos", null, null, null, CancellationToken.None)).Valor.Estudiantes.Items.Should().ContainSingle().Which.Id.Should().Be(_sinDni);
    }

    [Fact]
    public async Task No_acepta_mas_de_500_dni()
    {
        var dnis = string.Join(',', Enumerable.Range(10000000, 501));

        (await Servicio().InformeAsync(_relevamiento.Id, null, dnis, null, null, CancellationToken.None)).Error.Should().Be(ErroresAsistencia.DemasiadosDnis);
    }

    [Fact]
    public async Task El_avance_muestra_primero_las_materias_con_mas_pendientes()
    {
        var avance = (await Servicio().AvanceAsync(_relevamiento.Id, CancellationToken.None)).Valor;

        avance.Inscripciones.Should().Be(4);
        avance.Confirmadas.Should().Be(2);
        avance.Materias.Select(materia => (materia.Materia, materia.Inscriptos, materia.Confirmados)).Should().Equal(("Pedagogía", 2, 0), ("Didáctica", 2, 2));
        avance.Materias[0].Docentes.Should().BeEmpty("nadie tiene asignada Pedagogía: la dirección ve que falta asignarla");
    }
}
