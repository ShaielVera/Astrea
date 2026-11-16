using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Astrea.Api.Authentication;
using Astrea.Api.Authorization;
using Astrea.Api.Endpoints;
using Astrea.Api.Errors;
using Astrea.Api.Mantenimiento;
using Astrea.Application;
using Astrea.Application.Abstractions.Ai;
using Astrea.Application.Abstractions.Documents;
using Astrea.Application.Abstractions.Identity;
using Astrea.Application.Features.Knowledge;
using Astrea.Infrastructure;
using Astrea.Infrastructure.Persistence;
using Astrea.Infrastructure.Persistence.Seed;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("Astrea")
    ?? throw new InvalidOperationException("Falta la cadena de conexión 'ConnectionStrings:Astrea'.");

// PDFs emitidos: carpeta privada del servidor, fuera de lo que se publica por la web.
// Una ruta relativa se toma desde la carpeta de la API (no desde donde se lanzó el proceso).
var carpetaPrivada = Path.Combine(
    builder.Environment.ContentRootPath,
    builder.Configuration["Almacenamiento:CarpetaPrivada"] is { Length: > 0 } carpeta ? carpeta : Path.Combine("App_Data", "privado"));

// IA: proveedor de embeddings. La API key de Gemini va en user-secrets (Ia:Gemini:ApiKey), nunca en el repositorio.
var ia = builder.Configuration.GetSection(ConfiguracionIa.Seccion).Get<ConfiguracionIa>() ?? new ConfiguracionIa();
if (builder.Environment.IsProduction() && string.Equals(ia.Proveedor, ConfiguracionIa.ProveedorPrueba, StringComparison.OrdinalIgnoreCase))
{
    throw new InvalidOperationException("Los embeddings de prueba no se pueden usar en producción.");
}

builder.Services.AddApplication();
builder.Services.AddInfrastructure(connectionString, carpetaPrivada, ia);
builder.Services.AddSingleton(ia);
builder.Services.AddSingleton(
    builder.Configuration.GetSection(ConfiguracionIdentidad.Seccion).Get<ConfiguracionIdentidad>() ?? new ConfiguracionIdentidad());
builder.Services.AddSingleton(
    builder.Configuration.GetSection(ConfiguracionDocumentos.Seccion).Get<ConfiguracionDocumentos>() ?? new ConfiguracionDocumentos());

// La validación pública de documentos no pide sesión: se limita por IP para que no se pueda barrer códigos.
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy(LimitesDeUso.ValidacionPublica, contexto => RateLimitPartition.GetFixedWindowLimiter(
        contexto.Connection.RemoteIpAddress?.ToString() ?? "desconocida",
        _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = builder.Configuration.GetValue("LimitesDeUso:ValidacionesPorMinuto", 30),
            Window = TimeSpan.FromMinutes(1),
        }));

    // Publicar hallazgos y subir fotos: por usuario (uid de Firebase), para evitar abusos de almacenamiento.
    options.AddPolicy(LimitesDeUso.Publicaciones, contexto => RateLimitPartition.GetFixedWindowLimiter(
        contexto.User.FindFirst(FirebaseClaims.Uid)?.Value ?? contexto.Connection.RemoteIpAddress?.ToString() ?? "desconocida",
        _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = builder.Configuration.GetValue("LimitesDeUso:PublicacionesPorHora", 30),
            Window = TimeSpan.FromHours(1),
        }));

    // Preguntas al asistente: por usuario. Cada una consume cuota del proveedor de IA.
    options.AddPolicy(LimitesDeUso.Asistente, contexto => RateLimitPartition.GetFixedWindowLimiter(
        contexto.User.FindFirst(FirebaseClaims.Uid)?.Value ?? contexto.Connection.RemoteIpAddress?.ToString() ?? "desconocida",
        _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = builder.Configuration.GetValue("LimitesDeUso:PreguntasPorMinuto", 10),
            Window = TimeSpan.FromMinutes(1),
        }));
});

// Indexa en segundo plano los documentos que carga la Secretaría para el asistente.
builder.Services.AddSingleton<AvisoIngesta>();
builder.Services.AddSingleton<IAvisoIngesta>(servicios => servicios.GetRequiredService<AvisoIngesta>());
if (builder.Configuration.GetValue("Ingesta:ProcesarEnSegundoPlano", true))
{
    builder.Services.AddHostedService<ProcesadorIngesta>();
}

// Borra los hallazgos cerrados (con su foto y los datos de quien lo retiró) y las conversaciones inactivas, a los 30 días.
if (builder.Configuration.GetValue("Mantenimiento:DepuracionPeriodica", true))
{
    builder.Services.AddHostedService<DepuracionPeriodica>();
}

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUser, HttpCurrentUser>();
builder.Services.AddFirebaseAuthentication(builder.Configuration, builder.Environment);
builder.Services.AddPoliticasAstrea();

builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<ManejadorReglasDeNegocio>();
builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddOpenApi();

builder.Services.AddHealthChecks()
    .AddDbContextCheck<AppDbContext>("database", tags: ["ready"]);

var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
builder.Services.AddCors(options =>
    options.AddDefaultPolicy(policy =>
        // Content-Disposition expuesto: el frontend lee el nombre del PDF descargado.
        policy.WithOrigins(allowedOrigins).AllowAnyHeader().AllowAnyMethod().WithExposedHeaders("Content-Disposition")));

var app = builder.Build();

await PrepararBaseDeDatosAsync(app);

app.UseExceptionHandler();
app.UseStatusCodePages();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi().AllowAnonymous();
}
else if (!app.Environment.IsEnvironment(FirebaseAuthentication.AmbienteE2E))
{
    app.UseHttpsRedirection();
}

app.UseCors();
app.UseAuthentication();
app.UseAuthorization();

// Después de la autenticación: algunos límites se cuentan por usuario.
app.UseRateLimiter();

// live: la API está levantada. ready: además puede hablar con PostgreSQL.
app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false }).AllowAnonymous();
app.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = check => check.Tags.Contains("ready") }).AllowAnonymous();

var api = app.MapGroup("/api/v1");
api.MapGet("/", () => Results.Ok(new { name = "Astrea API", version = "v1" }))
    .WithName("GetApiInfo")
    .AllowAnonymous();
api.MapAuthEndpoints();
api.MapAdminEndpoints();
api.MapAcademicEndpoints();
api.MapExamEndpoints();
api.MapTramiteEndpoints();
api.MapHallazgoEndpoints();
api.MapDocumentoRagEndpoints();
api.MapAsistenteEndpoints();

app.Run();

// Solo para entornos locales: aplica migraciones y carga el catálogo del instituto. En E2E parte de una base vacía.
static async Task PrepararBaseDeDatosAsync(WebApplication app)
{
    var migrar = app.Configuration.GetValue<bool>("Database:MigrarAlIniciar");
    var recrear = app.Configuration.GetValue<bool>("Database:RecrearAlIniciar");
    var sembrar = app.Configuration.GetValue<bool>("Database:SembrarCatalogoInstitucional");
    if (!migrar && !recrear && !sembrar)
    {
        return;
    }

    if (recrear && !app.Environment.IsEnvironment(FirebaseAuthentication.AmbienteE2E))
    {
        throw new InvalidOperationException("'Database:RecrearAlIniciar' solo se permite en el ambiente E2E.");
    }

    await using var scope = app.Services.CreateAsyncScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    if (recrear)
    {
        await db.Database.EnsureDeletedAsync();
    }

    if (migrar || recrear)
    {
        await db.Database.MigrateAsync();
    }

    if (sembrar)
    {
        var resultado = await CatalogoInstitucionalSeeder.SembrarAsync(db, DateTimeOffset.UtcNow);
        foreach (var advertencia in resultado.Advertencias)
        {
            app.Logger.LogWarning("Catálogo institucional: {Advertencia}", advertencia);
        }
    }
}
