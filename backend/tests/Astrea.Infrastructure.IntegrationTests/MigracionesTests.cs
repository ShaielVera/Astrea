using Astrea.Domain.Users;
using AwesomeAssertions;
using Microsoft.EntityFrameworkCore;

namespace Astrea.Infrastructure.IntegrationTests;

[Collection(ColeccionBaseDeDatos.Nombre)]
public sealed class MigracionesTests(BaseDeDatosFixture baseDeDatos)
{
    [Fact]
    public async Task Las_migraciones_se_aplican_desde_una_base_limpia_y_habilitan_pgvector()
    {
        await using var db = baseDeDatos.CrearContexto();

        (await db.Database.GetPendingMigrationsAsync()).Should().BeEmpty();
        var extensiones = await db.Database
            .SqlQueryRaw<string>("SELECT extname AS \"Value\" FROM pg_extension")
            .ToListAsync();
        extensiones.Should().Contain("vector");
    }

    [Fact]
    public async Task El_catalogo_de_roles_queda_sembrado()
    {
        await using var db = baseDeDatos.CrearContexto();

        var codigos = await db.Roles.Select(rol => rol.Codigo).ToListAsync();

        codigos.Should().BeEquivalentTo(Rol.CodigoEstudiante, Rol.CodigoAdministrador);
    }
}
