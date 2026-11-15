using Astrea.Application.Abstractions.Persistence;
using Astrea.Domain.Users;
using Astrea.Infrastructure.Persistence;
using Astrea.Infrastructure.Persistence.Repositories;
using AwesomeAssertions;
using Microsoft.EntityFrameworkCore;

namespace Astrea.Infrastructure.IntegrationTests.Persistence;

[Collection(ColeccionBaseDeDatos.Nombre)]
public sealed class EfUserRepositoryTests(BaseDeDatosFixture baseDeDatos)
{
    private static readonly DateTimeOffset Ahora = new(2026, 11, 14, 12, 0, 0, TimeSpan.Zero);

    private static string UidUnico() => $"uid-{Guid.NewGuid():N}";

    private async Task GuardarAsync(params Usuario[] usuarios)
    {
        await using var db = baseDeDatos.CrearContexto();
        var repositorio = new EfUserRepository(db);
        foreach (var usuario in usuarios)
        {
            repositorio.Agregar(usuario);
        }

        await new EfUnitOfWork(db).GuardarCambiosAsync(CancellationToken.None);
    }

    [Fact]
    public async Task Guarda_y_recupera_un_usuario_con_sus_roles_por_uid()
    {
        var uid = UidUnico();
        var usuario = Usuario.Registrar(Guid.NewGuid(), uid, "alumna@isp20.edu.ar", Ahora);
        usuario.AsignarRol(Rol.Administrador, asignadoPor: null, Ahora);
        await GuardarAsync(usuario);

        await using var db = baseDeDatos.CrearContexto();
        var recuperado = await new EfUserRepository(db).ObtenerPorFirebaseUidAsync(uid, CancellationToken.None);

        recuperado.Should().NotBeNull();
        recuperado!.Id.Should().Be(usuario.Id);
        recuperado.Email.Should().Be("alumna@isp20.edu.ar");
        recuperado.CodigosDeRol().Should().Equal(Rol.CodigoAdministrador, Rol.CodigoEstudiante);
        recuperado.CreatedAt.Should().Be(Ahora);
    }

    [Fact]
    public async Task Un_uid_inexistente_devuelve_null()
    {
        await using var db = baseDeDatos.CrearContexto();

        var recuperado = await new EfUserRepository(db).ObtenerPorFirebaseUidAsync(UidUnico(), CancellationToken.None);

        recuperado.Should().BeNull();
    }

    [Fact]
    public async Task No_se_pueden_guardar_dos_usuarios_con_el_mismo_uid_de_firebase()
    {
        var uid = UidUnico();
        await GuardarAsync(Usuario.Registrar(Guid.NewGuid(), uid, "una@isp20.edu.ar", Ahora));

        var guardarDuplicado = () => GuardarAsync(Usuario.Registrar(Guid.NewGuid(), uid, "otra@isp20.edu.ar", Ahora));

        await guardarDuplicado.Should().ThrowAsync<RegistroDuplicadoException>();
    }

    [Fact]
    public async Task Cuenta_usuarios_activos_por_rol_sin_contar_desactivados()
    {
        await using (var limpio = baseDeDatos.CrearContexto())
        {
            await limpio.Usuarios.ExecuteDeleteAsync();
        }

        var admin = Usuario.Registrar(Guid.NewGuid(), UidUnico(), "admin@isp20.edu.ar", Ahora);
        admin.AsignarRol(Rol.Administrador, null, Ahora);
        var desactivado = Usuario.Registrar(Guid.NewGuid(), UidUnico(), "baja@isp20.edu.ar", Ahora);
        desactivado.Desactivar(Ahora);
        await GuardarAsync(
            admin,
            Usuario.Registrar(Guid.NewGuid(), UidUnico(), "a@isp20.edu.ar", Ahora),
            Usuario.Registrar(Guid.NewGuid(), UidUnico(), "b@isp20.edu.ar", Ahora),
            desactivado);

        await using var db = baseDeDatos.CrearContexto();
        var conteo = await new EfUserRepository(db).ContarActivosPorRolAsync(CancellationToken.None);

        conteo.Should().BeEquivalentTo(new Dictionary<string, int>
        {
            [Rol.CodigoEstudiante] = 3,
            [Rol.CodigoAdministrador] = 1,
        });
    }
}
