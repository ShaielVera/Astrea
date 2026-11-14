using Astrea.Api.Authentication;
using Astrea.Api.Authorization;
using Astrea.Api.Endpoints;
using Astrea.Application;
using Astrea.Application.Abstractions.Identity;
using Astrea.Infrastructure;
using Astrea.Infrastructure.Persistence;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("Astrea")
    ?? throw new InvalidOperationException("Falta la cadena de conexión 'ConnectionStrings:Astrea'.");

builder.Services.AddApplication();
builder.Services.AddInfrastructure(connectionString);
builder.Services.AddSingleton(
    builder.Configuration.GetSection(ConfiguracionIdentidad.Seccion).Get<ConfiguracionIdentidad>() ?? new ConfiguracionIdentidad());

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUser, HttpCurrentUser>();
builder.Services.AddFirebaseAuthentication(builder.Configuration, builder.Environment);
builder.Services.AddPoliticasAstrea();

builder.Services.AddProblemDetails();
builder.Services.AddOpenApi();

builder.Services.AddHealthChecks()
    .AddDbContextCheck<AppDbContext>("database", tags: ["ready"]);

var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
builder.Services.AddCors(options =>
    options.AddDefaultPolicy(policy =>
        policy.WithOrigins(allowedOrigins).AllowAnyHeader().AllowAnyMethod()));

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

// live: la API está levantada. ready: además puede hablar con PostgreSQL.
app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false }).AllowAnonymous();
app.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = check => check.Tags.Contains("ready") }).AllowAnonymous();

var api = app.MapGroup("/api/v1");
api.MapGet("/", () => Results.Ok(new { name = "Astrea API", version = "v1" }))
    .WithName("GetApiInfo")
    .AllowAnonymous();
api.MapAuthEndpoints();
api.MapAdminEndpoints();

app.Run();

// Solo para entornos locales: aplica migraciones al iniciar. En E2E además parte de una base vacía.
static async Task PrepararBaseDeDatosAsync(WebApplication app)
{
    var migrar = app.Configuration.GetValue<bool>("Database:MigrarAlIniciar");
    var recrear = app.Configuration.GetValue<bool>("Database:RecrearAlIniciar");
    if (!migrar && !recrear)
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

    await db.Database.MigrateAsync();
}
