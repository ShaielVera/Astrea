using Astrea.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;

namespace Astrea.Infrastructure.IntegrationTests;

/// <summary>
/// PostgreSQL + pgvector efímero, compartido por todas las clases de la colección.
/// Arranca vacío y se le aplican las migraciones una sola vez.
/// </summary>
public sealed class BaseDeDatosFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("pgvector/pgvector:pg17").Build();

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();
        await using var db = CrearContexto();
        await db.Database.MigrateAsync();
    }

    public Task DisposeAsync() => _postgres.DisposeAsync().AsTask();

    /// <summary>Un contexto nuevo por operación, como en una petición HTTP real.</summary>
    public AppDbContext CrearContexto()
    {
        var options = DependencyInjection
            .ConfigurarDbContext(new DbContextOptionsBuilder<AppDbContext>(), _postgres.GetConnectionString())
            .Options;

        return new AppDbContext((DbContextOptions<AppDbContext>)options);
    }
}

[CollectionDefinition(Nombre)]
public sealed class ColeccionBaseDeDatos : ICollectionFixture<BaseDeDatosFixture>
{
    public const string Nombre = "Base de datos";
}
