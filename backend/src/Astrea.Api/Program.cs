using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Astrea.Api.Authentication;
using Astrea.Api.Authorization;
using Astrea.Api.Endpoints;
using Astrea.Api.Errors;
using Astrea.Application;
using Astrea.Application.Abstractions.Documents;
using Astrea.Application.Abstractions.Identity;
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

builder.Services.AddApplication();
builder.Services.AddInfrastructure(connectionString, carpetaPrivada);
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
});

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
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

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
