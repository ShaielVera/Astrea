using Astrea.Domain.Common;
using Astrea.Domain.Users;
using AwesomeAssertions;

namespace Astrea.Domain.UnitTests.Users;

public class UsuarioTests
{
    private static readonly DateTimeOffset Ahora = new(2026, 11, 14, 12, 0, 0, TimeSpan.Zero);

    private static Usuario NuevoUsuario(string email = "alumna@isp20.edu.ar") =>
        Usuario.Registrar(Guid.NewGuid(), "uid-firebase-1", email, Ahora);

    [Fact]
    public void Un_usuario_nuevo_queda_activo_y_con_rol_estudiante()
    {
        var usuario = NuevoUsuario();

        usuario.Activo.Should().BeTrue();
        usuario.TieneRol(Rol.Estudiante).Should().BeTrue();
        usuario.TieneRol(Rol.Administrador).Should().BeFalse();
        usuario.CodigosDeRol().Should().Equal(Rol.CodigoEstudiante);
        usuario.CreatedAt.Should().Be(Ahora);
    }

    [Fact]
    public void El_alta_automatica_registra_la_asignacion_como_hecha_por_el_sistema()
    {
        var asignacion = NuevoUsuario().Roles.Single();

        asignacion.AsignadoPor.Should().BeNull();
        asignacion.AsignadoAt.Should().Be(Ahora);
    }

    [Fact]
    public void El_email_se_normaliza_en_minusculas_y_sin_espacios()
    {
        NuevoUsuario("  Alumna@ISP20.edu.AR ").Email.Should().Be("alumna@isp20.edu.ar");
    }

    [Theory]
    [InlineData("")]
    [InlineData("sin-arroba")]
    [InlineData("@isp20.edu.ar")]
    [InlineData("alumna@")]
    [InlineData("a@b@c")]
    public void No_se_puede_registrar_con_un_email_invalido(string email)
    {
        var registrar = () => NuevoUsuario(email);

        registrar.Should().Throw<DomainException>();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void No_se_puede_registrar_sin_uid_de_firebase(string uid)
    {
        var registrar = () => Usuario.Registrar(Guid.NewGuid(), uid, "alumna@isp20.edu.ar", Ahora);

        registrar.Should().Throw<DomainException>();
    }

    [Fact]
    public void Asignar_un_rol_que_ya_tiene_no_lo_duplica()
    {
        var usuario = NuevoUsuario();
        var adminId = Guid.NewGuid();

        usuario.AsignarRol(Rol.Administrador, adminId, Ahora.AddHours(1));
        usuario.AsignarRol(Rol.Administrador, adminId, Ahora.AddHours(2));

        usuario.Roles.Should().HaveCount(2);
        usuario.CodigosDeRol().Should().Equal(Rol.CodigoAdministrador, Rol.CodigoEstudiante);
        usuario.Roles.Single(r => r.RolId == Rol.Administrador.Id).AsignadoPor.Should().Be(adminId);
    }

    [Fact]
    public void Actualizar_el_email_cambia_la_fecha_de_modificacion_solo_si_cambio()
    {
        var usuario = NuevoUsuario();

        usuario.ActualizarEmail("ALUMNA@isp20.edu.ar", Ahora.AddDays(1));
        usuario.UpdatedAt.Should().Be(Ahora);

        usuario.ActualizarEmail("nueva@isp20.edu.ar", Ahora.AddDays(2));
        usuario.Email.Should().Be("nueva@isp20.edu.ar");
        usuario.UpdatedAt.Should().Be(Ahora.AddDays(2));
    }

    [Fact]
    public void Desactivar_deja_al_usuario_inactivo()
    {
        var usuario = NuevoUsuario();

        usuario.Desactivar(Ahora.AddDays(1));

        usuario.Activo.Should().BeFalse();
    }

    [Fact]
    public void El_catalogo_de_roles_tiene_estudiante_y_administrador_con_ids_fijos()
    {
        Rol.Catalogo.Select(rol => rol.Codigo).Should().BeEquivalentTo(Rol.CodigoEstudiante, Rol.CodigoAdministrador);
        Rol.PorId(Rol.Administrador.Id).Should().BeSameAs(Rol.Administrador);
        Rol.PorId(Guid.NewGuid()).Should().BeNull();
    }
}
