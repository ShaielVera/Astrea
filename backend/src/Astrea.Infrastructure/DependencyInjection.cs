using Astrea.Application.Abstractions.Clock;
using Astrea.Application.Abstractions.Persistence;
using Astrea.Infrastructure.Clock;
using Astrea.Infrastructure.Persistence;
using Astrea.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Astrea.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, string connectionString)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

        services.AddDbContext<AppDbContext>(options => ConfigurarDbContext(options, connectionString));

        services.AddScoped<IUserRepository, EfUserRepository>();
        services.AddScoped<IAcademicRepository, EfAcademicRepository>();
        services.AddScoped<IExamRepository, EfExamRepository>();
        services.AddScoped<ITramiteRepository, EfTramiteRepository>();
        services.AddScoped<IUnitOfWork, EfUnitOfWork>();
        services.AddSingleton<IClock, SystemClock>();

        return services;
    }

    /// <summary>Configuración única de EF Core, compartida con los tests de integración.</summary>
    public static DbContextOptionsBuilder ConfigurarDbContext(DbContextOptionsBuilder options, string connectionString) =>
        options
            .UseNpgsql(connectionString, npgsql => npgsql.UseVector())
            .UseSnakeCaseNamingConvention();
}
