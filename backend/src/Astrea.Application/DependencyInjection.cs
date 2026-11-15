using Astrea.Application.Features.Academic;
using Astrea.Application.Features.Auth;
using Astrea.Application.Features.Auth.IniciarSesion;
using Astrea.Application.Features.Auth.ObtenerPerfil;
using Astrea.Application.Features.Exams;
using Astrea.Application.Features.LostAndFound;
using Astrea.Application.Features.Procedures;
using Astrea.Application.Features.Users.ObtenerResumenUsuarios;
using Microsoft.Extensions.DependencyInjection;

namespace Astrea.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<IniciarSesionHandler>();
        services.AddScoped<ObtenerPerfilHandler>();
        services.AddScoped<ObtenerResumenUsuariosHandler>();
        services.AddScoped<ConsultasCatalogo>();
        services.AddScoped<AdministracionCatalogo>();
        services.AddScoped<Correlatividades>();
        services.AddScoped<ConsultasMesas>();
        services.AddScoped<AdministracionMesas>();
        services.AddScoped<UsuarioActual>();
        services.AddScoped<DetalleTramites>();
        services.AddScoped<SolicitudesTramite>();
        services.AddScoped<BandejaTramites>();
        services.AddScoped<EmisionDocumentos>();
        services.AddScoped<ValidacionDocumentos>();
        services.AddScoped<DatosOficiales>();
        services.AddScoped<PlantillasDocumento>();
        services.AddScoped<Hallazgos>();
        services.AddScoped<AdministracionHallazgos>();
        services.AddScoped<DepuracionHallazgos>();

        return services;
    }
}
