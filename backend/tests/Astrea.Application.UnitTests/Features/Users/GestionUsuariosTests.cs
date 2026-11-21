using Astrea.Application.Abstractions.Clock;
using Astrea.Application.Abstractions.Identity;
using Astrea.Application.Abstractions.Persistence;
using Astrea.Application.Contracts.Pagination;
using Astrea.Application.Contracts.Results;
using Astrea.Application.Features.Attendance;
using Astrea.Application.Features.Auth;
using Astrea.Application.Features.Users;
using Astrea.Domain.Users;
using AwesomeAssertions;
using NSubstitute;

namespace Astrea.Application.UnitTests.Features.Users;

public class GestionUsuariosTests
{
    private static readonly DateTimeOffset Ahora = new(2026, 11, 21, 15, 0, 0, TimeSpan.Zero);

    private readonly ICurrentUser _sesion = Substitute.For<ICurrentUser>();
    private readonly IUserRepository _usuarios = Substitute.For<IUserRepository>();
    private readonly IAsistenciaRepository _asistencia = Substitute.For<IAsistenciaRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly IClock _clock = Substitute.For<IClock>();
    private readonly Usuario _admin = Usuario.Registrar(Guid.NewGuid(), "uid-admin", "admin@astrea.test", Ahora);
    private readonly Usuario _estudiante = Usuario.Registrar(Guid.NewGuid(), "uid-estudiante", "estudiante@astrea.test", Ahora);

    public GestionUsuariosTests()
    {
        _clock.UtcNow.Returns(Ahora);
        _admin.AsignarRol(Rol.Administrador, null, Ahora);
        _sesion.FirebaseUid.Returns("uid-admin");
        _usuarios.ObtenerPorFirebaseUidAsync("uid-admin", Arg.Any<CancellationToken>()).Returns(_admin);
        _usuarios.ObtenerPorIdAsync(_admin.Id, Arg.Any<CancellationToken>()).Returns(_admin);
        _usuarios.ObtenerPorIdAsync(_estudiante.Id, Arg.Any<CancellationToken>()).Returns(_estudiante);
    }

    private GestionUsuarios Servicio()
    {
        var actual = new UsuarioActual(_sesion, _usuarios);
        return new GestionUsuarios(actual, _usuarios, _asistencia, new GestionDocentes(actual, _usuarios, _asistencia, _unitOfWork, _clock), _unitOfWork, _clock);
    }

    private void Administradores(int cantidad) =>
        _usuarios.ContarActivosPorRolAsync(Arg.Any<CancellationToken>()).Returns(new Dictionary<string, int> { [Rol.CodigoAdministrador] = cantidad });

    [Fact]
    public async Task Hace_administrador_a_cualquier_cuenta()
    {
        var resultado = await Servicio().DarRolAsync(_estudiante.Id, "administrador", CancellationToken.None);

        resultado.EsExitoso.Should().BeTrue();
        _estudiante.TieneRol(Rol.Administrador).Should().BeTrue();
        await _unitOfWork.Received(1).GuardarCambiosAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Quita_el_rol_de_administrador_a_otro()
    {
        _estudiante.AsignarRol(Rol.Administrador, _admin.Id, Ahora);
        Administradores(2);

        var resultado = await Servicio().QuitarRolAsync(_estudiante.Id, Rol.CodigoAdministrador, CancellationToken.None);

        resultado.EsExitoso.Should().BeTrue();
        _estudiante.TieneRol(Rol.Administrador).Should().BeFalse();
    }

    [Fact]
    public async Task Nadie_se_quita_a_si_mismo_el_rol_de_administrador()
    {
        Administradores(2);

        var resultado = await Servicio().QuitarRolAsync(_admin.Id, Rol.CodigoAdministrador, CancellationToken.None);

        resultado.Error.Should().Be(ErroresUsuarios.QuitarseAdministrador);
        _admin.TieneRol(Rol.Administrador).Should().BeTrue();
    }

    [Fact]
    public async Task No_se_deja_al_sistema_sin_administradores()
    {
        _estudiante.AsignarRol(Rol.Administrador, null, Ahora);
        Administradores(1);

        var resultado = await Servicio().QuitarRolAsync(_estudiante.Id, Rol.CodigoAdministrador, CancellationToken.None);

        resultado.Error.Should().Be(ErroresUsuarios.UltimoAdministrador);
    }

    [Fact]
    public async Task El_rol_de_estudiante_no_se_toca_desde_aca()
    {
        var resultado = await Servicio().QuitarRolAsync(_estudiante.Id, Rol.CodigoEstudiante, CancellationToken.None);

        resultado.Error.Should().Be(ErroresUsuarios.RolInvalido);
    }

    [Fact]
    public async Task Hacer_docente_usa_la_gestion_de_docentes()
    {
        var resultado = await Servicio().DarRolAsync(_estudiante.Id, Rol.CodigoDocente, CancellationToken.None);

        resultado.EsExitoso.Should().BeTrue();
        _estudiante.TieneRol(Rol.Docente).Should().BeTrue();
    }

    [Fact]
    public async Task Lista_las_cuentas_con_sus_roles_y_las_materias_de_los_docentes()
    {
        _estudiante.AsignarRol(Rol.Docente, _admin.Id, Ahora);
        _estudiante.RegistrarIngreso(Ahora);
        var materia = new AsignacionDto(Guid.NewGuid(), Guid.NewGuid(), "Programación I", 2, "TSDS");
        _usuarios.ListarCuentasAsync("vera", Arg.Any<PageRequest>(), Arg.Any<CancellationToken>())
            .Returns(PagedResult<Usuario>.From([_estudiante, _admin], PageRequest.Create(1, 20), 2));
        _asistencia.ListarDocentesAsync(Arg.Any<CancellationToken>()).Returns([new DocenteDto(_estudiante.Id, "x", "x", [materia])]);

        var resultado = await Servicio().ListarAsync("  vera ", null, null, CancellationToken.None);

        resultado.Valor.Items.Should().HaveCount(2);
        var docente = resultado.Valor.Items[0];
        docente.Roles.Should().BeEquivalentTo([Rol.CodigoDocente, Rol.CodigoEstudiante]);
        docente.UltimoIngresoEn.Should().Be(Ahora);
        docente.Materias.Should().ContainSingle().Which.Materia.Should().Be("Programación I");
        resultado.Valor.Items[1].Materias.Should().BeEmpty();
    }

    [Fact]
    public async Task Una_busqueda_demasiado_larga_se_rechaza()
    {
        var resultado = await Servicio().ListarAsync(new string('a', 101), null, null, CancellationToken.None);

        resultado.Error!.Tipo.Should().Be(TipoError.Validacion);
    }
}
