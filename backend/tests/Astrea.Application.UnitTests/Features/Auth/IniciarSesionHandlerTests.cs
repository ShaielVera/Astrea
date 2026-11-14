using Astrea.Application.Abstractions.Clock;
using Astrea.Application.Abstractions.Identity;
using Astrea.Application.Abstractions.Persistence;
using Astrea.Application.Contracts.Results;
using Astrea.Application.Features.Auth;
using Astrea.Application.Features.Auth.IniciarSesion;
using Astrea.Domain.Users;
using AwesomeAssertions;
using NSubstitute;

namespace Astrea.Application.UnitTests.Features.Auth;

public class IniciarSesionHandlerTests
{
    private static readonly DateTimeOffset Ahora = new(2026, 11, 14, 12, 0, 0, TimeSpan.Zero);

    private readonly ICurrentUser _currentUser = Substitute.For<ICurrentUser>();
    private readonly IUserRepository _usuarios = Substitute.For<IUserRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly IClock _clock = Substitute.For<IClock>();
    private ConfiguracionIdentidad _configuracion = new() { AdministradoresIniciales = ["bedelia@isp20.edu.ar"] };

    public IniciarSesionHandlerTests()
    {
        _clock.UtcNow.Returns(Ahora);
    }

    private IniciarSesionHandler CrearHandler() => new(_currentUser, _usuarios, _unitOfWork, _clock, _configuracion);

    private void ConSesion(string uid, string? email, bool emailVerificado = true)
    {
        _currentUser.FirebaseUid.Returns(uid);
        _currentUser.Email.Returns(email);
        _currentUser.EmailVerificado.Returns(emailVerificado);
    }

    [Fact]
    public async Task La_primera_vez_registra_al_usuario_como_estudiante_y_guarda()
    {
        ConSesion("uid-1", "alumna@isp20.edu.ar");

        var resultado = await CrearHandler().HandleAsync(CancellationToken.None);

        resultado.EsExitoso.Should().BeTrue();
        resultado.Valor.Email.Should().Be("alumna@isp20.edu.ar");
        resultado.Valor.Roles.Should().Equal(Rol.CodigoEstudiante);
        _usuarios.Received(1).Agregar(Arg.Is<Usuario>(u => u.FirebaseUid == "uid-1"));
        await _unitOfWork.Received(1).GuardarCambiosAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Si_el_usuario_ya_existe_no_lo_vuelve_a_agregar_y_sincroniza_el_email()
    {
        var existente = Usuario.Registrar(Guid.NewGuid(), "uid-1", "viejo@isp20.edu.ar", Ahora.AddDays(-10));
        _usuarios.ObtenerPorFirebaseUidAsync("uid-1", Arg.Any<CancellationToken>()).Returns(existente);
        ConSesion("uid-1", "nuevo@isp20.edu.ar");

        var resultado = await CrearHandler().HandleAsync(CancellationToken.None);

        resultado.Valor.Id.Should().Be(existente.Id);
        resultado.Valor.Email.Should().Be("nuevo@isp20.edu.ar");
        _usuarios.DidNotReceive().Agregar(Arg.Any<Usuario>());
    }

    [Fact]
    public async Task Un_administrador_inicial_con_email_verificado_recibe_el_rol_administrador()
    {
        ConSesion("uid-admin", "Bedelia@ISP20.edu.ar", emailVerificado: true);

        var resultado = await CrearHandler().HandleAsync(CancellationToken.None);

        resultado.Valor.Roles.Should().BeEquivalentTo(Rol.CodigoEstudiante, Rol.CodigoAdministrador);
    }

    [Fact]
    public async Task Un_administrador_inicial_sin_email_verificado_queda_solo_como_estudiante()
    {
        ConSesion("uid-falso", "bedelia@isp20.edu.ar", emailVerificado: false);

        var resultado = await CrearHandler().HandleAsync(CancellationToken.None);

        resultado.Valor.Roles.Should().Equal(Rol.CodigoEstudiante);
    }

    [Fact]
    public async Task Un_email_que_no_esta_en_la_lista_no_recibe_rol_administrador()
    {
        _configuracion = new ConfiguracionIdentidad();
        ConSesion("uid-1", "bedelia@isp20.edu.ar");

        var resultado = await CrearHandler().HandleAsync(CancellationToken.None);

        resultado.Valor.Roles.Should().Equal(Rol.CodigoEstudiante);
    }

    [Fact]
    public async Task Una_cuenta_desactivada_no_puede_iniciar_sesion()
    {
        var existente = Usuario.Registrar(Guid.NewGuid(), "uid-1", "alumna@isp20.edu.ar", Ahora);
        existente.Desactivar(Ahora);
        _usuarios.ObtenerPorFirebaseUidAsync("uid-1", Arg.Any<CancellationToken>()).Returns(existente);
        ConSesion("uid-1", "alumna@isp20.edu.ar");

        var resultado = await CrearHandler().HandleAsync(CancellationToken.None);

        resultado.Error.Should().Be(ErroresAuth.CuentaDesactivada);
        await _unitOfWork.DidNotReceive().GuardarCambiosAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Sin_identidad_devuelve_no_autenticado()
    {
        _currentUser.FirebaseUid.Returns((string?)null);

        var resultado = await CrearHandler().HandleAsync(CancellationToken.None);

        resultado.Error!.Tipo.Should().Be(TipoError.NoAutenticado);
    }

    [Fact]
    public async Task Una_cuenta_sin_email_devuelve_error_de_validacion()
    {
        ConSesion("uid-1", email: null);

        var resultado = await CrearHandler().HandleAsync(CancellationToken.None);

        resultado.Error.Should().Be(ErroresAuth.SinEmail);
    }
}
