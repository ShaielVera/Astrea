using Astrea.Application.Abstractions.Persistence;
using Astrea.Domain.Users;

namespace Astrea.Application.Features.Users.ObtenerResumenUsuarios;

public sealed record ResumenUsuariosDto(int Estudiantes, int Administradores, int Docentes);

/// <summary>Resumen agregado de usuarios activos para el panel de administración.</summary>
public sealed class ObtenerResumenUsuariosHandler(IUserRepository usuarios)
{
    public async Task<ResumenUsuariosDto> HandleAsync(CancellationToken cancellationToken)
    {
        var conteo = await usuarios.ContarActivosPorRolAsync(cancellationToken);

        return new ResumenUsuariosDto(
            conteo.GetValueOrDefault(Rol.CodigoEstudiante),
            conteo.GetValueOrDefault(Rol.CodigoAdministrador),
            conteo.GetValueOrDefault(Rol.CodigoDocente));
    }
}
