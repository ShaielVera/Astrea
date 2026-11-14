using Astrea.Application.Abstractions.Identity;
using Astrea.Application.Abstractions.Persistence;
using Astrea.Application.Features.Auth;
using Astrea.Application.Features.Auth.ObtenerPerfil;
using Astrea.Domain.Users;
using AwesomeAssertions;
using NSubstitute;

namespace Astrea.Application.UnitTests.Features.Auth;

public class ObtenerPerfilHandlerTests
{
    private static readonly DateTimeOffset Ahora = new(2026, 11, 14, 12, 0, 0, TimeSpan.Zero);

    private readonly ICurrentUser _currentUser = Substitute.For<ICurrentUser>();
    private readonly IUserRepository _usuarios = Substitute.For<IUserRepository>();

    private ObtenerPerfilHandler CrearHandler() => new(_currentUser, _usuarios);

    [Fact]
    public async Task Devuelve_el_perfil_del_usuario_autenticado()
    {
        var usuario = Usuario.Registrar(Guid.NewGuid(), "uid-1", "alumna@isp20.edu.ar", Ahora);
        _currentUser.FirebaseUid.Returns("uid-1");
        _usuarios.ObtenerPorFirebaseUidAsync("uid-1", Arg.Any<CancellationToken>()).Returns(usuario);

        var resultado = await CrearHandler().HandleAsync(CancellationToken.None);

        resultado.Valor.Should().BeEquivalentTo(new PerfilDto(usuario.Id, "alumna@isp20.edu.ar", null, null, [Rol.CodigoEstudiante]));
    }

    [Fact]
    public async Task Si_todavia_no_inicio_sesion_en_Astrea_devuelve_no_registrado()
    {
        _currentUser.FirebaseUid.Returns("uid-desconocido");

        var resultado = await CrearHandler().HandleAsync(CancellationToken.None);

        resultado.Error.Should().Be(ErroresAuth.NoRegistrado);
    }

    [Fact]
    public async Task Una_cuenta_desactivada_no_puede_ver_su_perfil()
    {
        var usuario = Usuario.Registrar(Guid.NewGuid(), "uid-1", "alumna@isp20.edu.ar", Ahora);
        usuario.Desactivar(Ahora);
        _currentUser.FirebaseUid.Returns("uid-1");
        _usuarios.ObtenerPorFirebaseUidAsync("uid-1", Arg.Any<CancellationToken>()).Returns(usuario);

        var resultado = await CrearHandler().HandleAsync(CancellationToken.None);

        resultado.Error.Should().Be(ErroresAuth.CuentaDesactivada);
    }
}
