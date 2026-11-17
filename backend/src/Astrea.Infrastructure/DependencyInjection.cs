using Astrea.Application.Abstractions.Ai;
using Astrea.Application.Abstractions.Clock;
using Astrea.Application.Abstractions.Documents;
using Astrea.Application.Abstractions.Notifications;
using Astrea.Application.Abstractions.Persistence;
using Astrea.Infrastructure.Ai;
using Astrea.Infrastructure.Clock;
using Astrea.Infrastructure.Documents;
using Astrea.Infrastructure.Mensajeria;
using Astrea.Infrastructure.Persistence;
using Astrea.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Astrea.Infrastructure;

public static class DependencyInjection
{
    /// <param name="carpetaArchivosPrivados">Carpeta del servidor donde se guardan los PDF emitidos (nunca publicada por la web).</param>
    /// <param name="ia">Proveedor de IA (embeddings y modelo del asistente): Gemini (real) o Prueba (local, para tests).</param>
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, string connectionString, string carpetaArchivosPrivados, ConfiguracionIa ia)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        ArgumentException.ThrowIfNullOrWhiteSpace(carpetaArchivosPrivados);

        services.AddDbContext<AppDbContext>(options => ConfigurarDbContext(options, connectionString));

        services.AddScoped<IUserRepository, EfUserRepository>();
        services.AddScoped<IAcademicRepository, EfAcademicRepository>();
        services.AddScoped<IExamRepository, EfExamRepository>();
        services.AddScoped<ITramiteRepository, EfTramiteRepository>();
        services.AddScoped<IObjetoPerdidoRepository, EfObjetoPerdidoRepository>();
        services.AddScoped<IDocumentoRagRepository, EfDocumentoRagRepository>();
        services.AddScoped<IConversacionRepository, EfConversacionRepository>();
        services.AddScoped<ITelemetriaAsistente, EfTelemetriaAsistente>();
        services.AddScoped<IAsistenciaRepository, EfAsistenciaRepository>();
        services.AddScoped<IUnitOfWork, EfUnitOfWork>();
        services.AddSingleton<IClock, SystemClock>();
        services.AddSingleton<IPdfGenerator, QuestPdfGenerator>();
        services.AddSingleton<IFileStorage>(new CarpetaPrivadaStorage(carpetaArchivosPrivados));
        services.AddSingleton<IExtractorTextoPdf, PdfPigExtractor>();

        if (string.Equals(ia.Proveedor, ConfiguracionIa.ProveedorPrueba, StringComparison.OrdinalIgnoreCase))
        {
            services.AddSingleton<IEmbeddingService, EmbeddingsDePrueba>();
            services.AddSingleton<IModeloLenguaje, ModeloDePrueba>();
        }
        else if (string.Equals(ia.Proveedor, ConfiguracionIa.ProveedorGemini, StringComparison.OrdinalIgnoreCase))
        {
            services.AddSingleton<IEmbeddingService>(new GeminiEmbeddingService(new HttpClient { Timeout = TimeSpan.FromSeconds(60) }, ia.Gemini));
            services.AddSingleton<IModeloLenguaje>(new GeminiModeloLenguaje(new HttpClient { Timeout = TimeSpan.FromSeconds(45) }, ia.Gemini));
        }
        else
        {
            throw new InvalidOperationException($"Proveedor de IA desconocido: '{ia.Proveedor}'. Usá 'Gemini' o 'Prueba'.");
        }

        return services;
    }

    /// <summary>Envío de códigos de verificación (email por SMTP; modo demostración solo fuera de producción).</summary>
    public static IServiceCollection AddMensajeria(this IServiceCollection services, ConfiguracionMensajeria configuracion)
    {
        services.AddSingleton(configuracion);
        services.AddSingleton<IEnviadorCodigos, EnviadorCodigos>();
        return services;
    }

    /// <summary>Configuración única de EF Core, compartida con los tests de integración.</summary>
    public static DbContextOptionsBuilder ConfigurarDbContext(DbContextOptionsBuilder options, string connectionString) =>
        options
            .UseNpgsql(connectionString, npgsql => npgsql.UseVector())
            .UseSnakeCaseNamingConvention();
}
