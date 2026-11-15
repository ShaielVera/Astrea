using System.Net.Http.Headers;
using Astrea.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Testcontainers.PostgreSql;

namespace Astrea.Api.IntegrationTests.Infrastructure;

/// <summary>
/// Levanta la API completa contra un PostgreSQL + pgvector efímero en Docker.
/// La autenticación es la real (JwtBearer) pero confía en <see cref="TokensDePrueba.ClaveConfiable"/>.
/// </summary>
public sealed class AstreaApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    public const string AdministradorInicial = "bedelia@astrea.test";

    public const string UrlValidacion = "https://astrea.test/validar/{codigo}";

    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("pgvector/pgvector:pg17").Build();

    /// <summary>Carpeta privada temporal para los PDF emitidos durante los tests.</summary>
    public string CarpetaArchivos { get; } = Path.Combine(Path.GetTempPath(), $"astrea-tests-{Guid.NewGuid():N}");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:Astrea", _postgres.GetConnectionString());
        builder.UseSetting("Almacenamiento:CarpetaPrivada", CarpetaArchivos);
        builder.UseSetting("Documentos:UrlValidacion", UrlValidacion);
        builder.UseSetting("Firebase:ProjectId", TokensDePrueba.ProjectId);
        builder.UseSetting("Identidad:AdministradoresIniciales:0", AdministradorInicial);

        builder.ConfigureTestServices(services =>
            services.PostConfigure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, options =>
            {
                // Evita descargar las claves de Google: los tokens de prueba se firman localmente.
                options.Configuration = new OpenIdConnectConfiguration();
                options.TokenValidationParameters.IssuerSigningKey = TokensDePrueba.ClaveConfiable;
            }));
    }

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();

        using var scope = Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.MigrateAsync();
    }

    public new async Task DisposeAsync()
    {
        await base.DisposeAsync();
        await _postgres.DisposeAsync();
        if (Directory.Exists(CarpetaArchivos))
        {
            Directory.Delete(CarpetaArchivos, recursive: true);
        }
    }

    public HttpClient CrearClienteCon(string token)
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    public async Task EjecutarEnBaseAsync(Func<AppDbContext, Task> accion)
    {
        using var scope = Services.CreateScope();
        await accion(scope.ServiceProvider.GetRequiredService<AppDbContext>());
    }
}

[CollectionDefinition(Nombre)]
public sealed class ColeccionApi : ICollectionFixture<AstreaApiFactory>
{
    public const string Nombre = "API";
}
