using Astrea.Application.Features.Profile;
using Astrea.Domain.Users;

namespace Astrea.Api.Endpoints;

/// <summary>"Mi perfil": datos personales (los confirma Secretaría) y email y celular de contacto validados con un código.</summary>
internal static class PerfilEndpoints
{
    public static RouteGroupBuilder MapPerfilEndpoints(this RouteGroupBuilder api)
    {
        // Cualquier usuario con sesión (la política por defecto exige sesión): cada uno ve y cambia solo lo suyo.
        var perfil = api.MapGroup("/me/perfil").WithTags("Mi perfil").ProducesProblem(StatusCodes.Status400BadRequest);

        perfil.MapGet("", async (Perfil servicio, CancellationToken ct) => (await servicio.ObtenerAsync(ct)).AHttp())
            .WithName("ObtenerMiPerfil")
            .Produces<MiPerfilDto>();

        perfil.MapPut("/datos", async (DatosPersonalesCommand comando, Perfil servicio, CancellationToken ct) =>
                (await servicio.GuardarDatosAsync(comando, ct)).AHttp())
            .WithName("GuardarMisDatos")
            .WithDescription("Nombre, apellido y DNI. Una vez que Secretaría los confirma, solo ella los cambia.")
            .Produces<MiPerfilDto>()
            .ProducesProblem(StatusCodes.Status409Conflict);

        perfil.MapPut("/contacto", async (ContactoCommand comando, Perfil servicio, CancellationToken ct) =>
                (await servicio.GuardarContactoAsync(comando, ct)).AHttp())
            .WithName("GuardarMiContacto")
            .WithDescription("Email y celular de contacto. Si cambian, hay que volver a validarlos.")
            .Produces<MiPerfilDto>();

        perfil.MapPost("/verificaciones/{canal}", async (string canal, Perfil servicio, CancellationToken ct) =>
                Canal(canal) is { } valor ? (await servicio.EnviarCodigoAsync(valor, ct)).AHttp() : Results.NotFound())
            .WithName("EnviarCodigoDeVerificacion")
            .WithDescription("canal: email o celular. Manda un código de 6 dígitos que vence a los 10 minutos (uno por minuto como máximo).")
            .Produces<CodigoEnviadoDto>()
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable);

        perfil.MapPost("/verificaciones/{canal}/confirmacion", async (string canal, ValidarCodigoCommand comando, Perfil servicio, CancellationToken ct) =>
                Canal(canal) is { } valor ? (await servicio.ValidarCodigoAsync(valor, comando, ct)).AHttp() : Results.NotFound())
            .WithName("ValidarCodigoDeVerificacion")
            .Produces<MiPerfilDto>();

        return api;
    }

    private static CanalContacto? Canal(string canal) => canal.ToLowerInvariant() switch
    {
        "email" => CanalContacto.Email,
        "celular" => CanalContacto.Celular,
        _ => null,
    };
}
