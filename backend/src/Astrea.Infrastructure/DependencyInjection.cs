using Astrea.Application.Abstractions.Clock;
using Astrea.Application.Abstractions.Documents;
using Astrea.Application.Abstractions.Persistence;
using Astrea.Infrastructure.Clock;
using Astrea.Infrastructure.Documents;
using Astrea.Infrastructure.Persistence;
using Astrea.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Astrea.Infrastructure;

public static class DependencyInjection
{
    /// <param name="carpetaArchivosPrivados">Carpeta del servidor donde se guardan los PDF emitidos (nunca publicada por la web).</param>
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, string connectionString, string carpetaArchivosPrivados)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        ArgumentException.ThrowIfNullOrWhiteSpace(carpetaArchivosPrivados);

        services.AddDbContext<AppDbContext>(options => ConfigurarDbContext(options, connectionString));

        services.AddScoped<IUserRepository, EfUserRepository>();
        services.AddScoped<IAcademicRepository, EfAcademicRepository>();
        services.AddScoped<IExamRepository, EfExamRepository>();
        services.AddScoped<ITramiteRepository, EfTramiteRepository>();
        services.AddScoped<IObjetoPerdidoRepository, EfObjetoPerdidoRepository>();
        services.AddScoped<IUnitOfWork, EfUnitOfWork>();
        services.AddSingleton<IClock, SystemClock>();
        services.AddSingleton<IPdfGenerator, QuestPdfGenerator>();
        services.AddSingleton<IFileStorage>(new CarpetaPrivadaStorage(carpetaArchivosPrivados));

        return services;
    }

    /// <summary>Configuración única de EF Core, compartida con los tests de integración.</summary>
    public static DbContextOptionsBuilder ConfigurarDbContext(DbContextOptionsBuilder options, string connectionString) =>
        options
            .UseNpgsql(connectionString, npgsql => npgsql.UseVector())
            .UseSnakeCaseNamingConvention();
}
