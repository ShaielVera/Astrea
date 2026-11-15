using Astrea.Application.Features.Academic;
using Astrea.Application.Features.Auth.IniciarSesion;
using Astrea.Application.Features.Auth.ObtenerPerfil;
using Astrea.Application.Features.Exams;
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

        return services;
    }
}
