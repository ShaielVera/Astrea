using Astrea.Domain.Users;

namespace Astrea.Application.Features.Auth;

public sealed record PerfilDto(Guid Id, string Email, string? Nombre, string? Apellido, IReadOnlyList<string> Roles)
{
    public static PerfilDto Desde(Usuario usuario) =>
        new(usuario.Id, usuario.Email, usuario.Nombre, usuario.Apellido, usuario.CodigosDeRol());
}
