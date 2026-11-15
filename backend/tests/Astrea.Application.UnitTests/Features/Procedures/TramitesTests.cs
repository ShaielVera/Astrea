using Astrea.Application.Abstractions.Clock;
using Astrea.Application.Abstractions.Documents;
using Astrea.Application.Abstractions.Identity;
using Astrea.Application.Abstractions.Persistence;
using Astrea.Application.Contracts.Results;
using Astrea.Application.Features.Auth;
using Astrea.Application.Features.Procedures;
using Astrea.Domain.Academic;
using Astrea.Domain.Procedures;
using Astrea.Domain.Users;
using AwesomeAssertions;
using NSubstitute;

namespace Astrea.Application.UnitTests.Features.Procedures;

public class TramitesTests
{
    private static readonly DateTimeOffset Ahora = new(2026, 11, 15, 12, 0, 0, TimeSpan.Zero);

    private readonly ICurrentUser _sesion = Substitute.For<ICurrentUser>();
    private readonly IUserRepository _usuarios = Substitute.For<IUserRepository>();
    private readonly ITramiteRepository _tramites = Substitute.For<ITramiteRepository>();
    private readonly IAcademicRepository _academico = Substitute.For<IAcademicRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly IClock _clock = Substitute.For<IClock>();
    private readonly Usuario _estudiante = Usuario.Registrar(Guid.NewGuid(), "uid-estudiante", "ana@astrea.test", Ahora);
    private readonly Usuario _otro = Usuario.Registrar(Guid.NewGuid(), "uid-otro", "otro@astrea.test", Ahora);
    private readonly Carrera _carrera = Carrera.Crear(Guid.NewGuid(), "TSDS", "Desarrollo de Software", 3, Ahora);
    private readonly TramiteTipo _tipo = CatalogoTramites.Tipos().Single(tipo => tipo.Id == CatalogoTramites.AlumnoRegularId);

    public TramitesTests()
    {
        _clock.UtcNow.Returns(Ahora);
        _sesion.FirebaseUid.Returns("uid-estudiante");
        _usuarios.ObtenerPorFirebaseUidAsync("uid-estudiante", Arg.Any<CancellationToken>()).Returns(_estudiante);
        _tramites.ObtenerTipoAsync(_tipo.Id, Arg.Any<CancellationToken>()).Returns(_tipo);
        _academico.ObtenerCarreraAsync(_carrera.Id, Arg.Any<CancellationToken>()).Returns(_carrera);
        _tramites.ObtenerPersonasAsync(Arg.Any<IEnumerable<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(call => call.Arg<IEnumerable<Guid>>().ToDictionary(id => id, id => new PersonaDto(id, $"{id}@astrea.test", null, null, null)));
    }

    private SolicitudesTramite Solicitudes() =>
        new(new UsuarioActual(_sesion, _usuarios), _tramites, new DetalleTramites(_tramites, _academico, new ConfiguracionDocumentos()), Substitute.For<IFileStorage>(), _unitOfWork, _clock);

    private Dictionary<string, string?> Datos(Guid? carreraId = null) =>
        new() { ["carrera"] = (carreraId ?? _carrera.Id).ToString(), ["presentarAnte"] = "ANSES" };

    [Fact]
    public async Task Solicitar_crea_el_tramite_a_nombre_del_usuario_de_la_sesion()
    {
        var resultado = await Solicitudes().SolicitarAsync(new SolicitarTramiteCommand(_tipo.Id, Datos(), null), CancellationToken.None);

        resultado.Valor.Estado.Should().Be(EstadoTramite.Pendiente);
        resultado.Valor.Datos.Should().ContainSingle(dato => dato.Clave == "carrera").Which.Valor.Should().Be("Desarrollo de Software");
        resultado.Valor.Historial.Should().ContainSingle().Which.Actor.Should().Be("Vos");
        _tramites.Received(1).Agregar(Arg.Is<Tramite>(tramite => tramite.UsuarioId == _estudiante.Id));
        await _unitOfWork.Received(1).GuardarCambiosAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Una_carrera_inexistente_o_inactiva_se_rechaza()
    {
        var resultado = await Solicitudes().SolicitarAsync(new SolicitarTramiteCommand(_tipo.Id, Datos(Guid.NewGuid()), null), CancellationToken.None);

        resultado.Error.Should().Be(ErroresTramites.CarreraInvalida);
        _tramites.DidNotReceiveWithAnyArgs().Agregar(default(Tramite)!);
    }

    [Fact]
    public async Task La_misma_clave_de_idempotencia_devuelve_el_tramite_ya_creado()
    {
        var clave = Guid.NewGuid();
        var existente = Tramite.Solicitar(Guid.NewGuid(), _estudiante.Id, _tipo, Datos(), clave, Ahora);
        _tramites.ObtenerPorClaveIdempotenciaAsync(_estudiante.Id, clave, Arg.Any<CancellationToken>()).Returns(existente);

        var resultado = await Solicitudes().SolicitarAsync(new SolicitarTramiteCommand(_tipo.Id, Datos(), clave), CancellationToken.None);

        resultado.Valor.Id.Should().Be(existente.Id);
        _tramites.DidNotReceiveWithAnyArgs().Agregar(default(Tramite)!);
        await _unitOfWork.DidNotReceive().GuardarCambiosAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Un_tramite_ajeno_se_responde_como_inexistente()
    {
        var ajeno = Tramite.Solicitar(Guid.NewGuid(), _otro.Id, _tipo, Datos(), null, Ahora);
        _tramites.ObtenerAsync(ajeno.Id, Arg.Any<CancellationToken>()).Returns(ajeno);

        var resultado = await Solicitudes().ObtenerMioAsync(ajeno.Id, CancellationToken.None);

        resultado.Error.Should().Be(ErroresTramites.TramiteNoEncontrado);
    }

    [Fact]
    public async Task El_estudiante_ve_a_Secretaria_como_actor_y_no_al_administrador()
    {
        var tramite = Tramite.Solicitar(Guid.NewGuid(), _estudiante.Id, _tipo, Datos(), null, Ahora);
        tramite.Rechazar(Guid.NewGuid(), "Faltan datos", Ahora.AddMinutes(5));
        _tramites.ObtenerAsync(tramite.Id, Arg.Any<CancellationToken>()).Returns(tramite);

        var resultado = await Solicitudes().ObtenerMioAsync(tramite.Id, CancellationToken.None);

        resultado.Valor.Historial.Select(evento => evento.Actor).Should().Equal("Vos", "Secretaría");
        resultado.Valor.AdministradorAsignado.Should().BeNull();
        resultado.Valor.ObservacionAdministrativa.Should().Be("Faltan datos");
    }

    [Fact]
    public async Task Sin_sesion_no_se_puede_solicitar()
    {
        _sesion.FirebaseUid.Returns((string?)null);

        var resultado = await Solicitudes().SolicitarAsync(new SolicitarTramiteCommand(_tipo.Id, Datos(), null), CancellationToken.None);

        resultado.Error!.Tipo.Should().Be(TipoError.NoAutenticado);
    }

    [Fact]
    public async Task Una_cuenta_desactivada_no_puede_solicitar()
    {
        _estudiante.Desactivar(Ahora);

        var resultado = await Solicitudes().SolicitarAsync(new SolicitarTramiteCommand(_tipo.Id, Datos(), null), CancellationToken.None);

        resultado.Error.Should().Be(ErroresAuth.CuentaDesactivada);
    }
}
