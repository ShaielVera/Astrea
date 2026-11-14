using System.Security.Claims;
using Astrea.Api.Authentication;
using Astrea.Application.Abstractions.Persistence;
using Microsoft.AspNetCore.Authentication;

namespace Astrea.Api.Authorization;

/// <summary>
/// Agrega al usuario autenticado los roles guardados en la base de datos.
/// La fuente de verdad de los roles es Astrea, no el token ni el cliente.
/// Un usuario desactivado o todavía no registrado queda sin roles.
/// </summary>
internal sealed class RolesDesdeBaseDeDatos(IUserRepository usuarios) : IClaimsTransformation
{
    private const string Marca = "astrea_roles_cargados";

    public async Task<ClaimsPrincipal> TransformAsync(ClaimsPrincipal principal)
    {
        if (principal.Identity is not ClaimsIdentity { IsAuthenticated: true } identidad || principal.HasClaim(Marca, "1"))
        {
            return principal;
        }

        var uid = identidad.FindFirst(FirebaseClaims.Uid)?.Value;
        var roles = new ClaimsIdentity();
        roles.AddClaim(new Claim(Marca, "1"));

        if (!string.IsNullOrWhiteSpace(uid))
        {
            var usuario = await usuarios.ObtenerPorFirebaseUidAsync(uid, CancellationToken.None);
            if (usuario is { Activo: true })
            {
                foreach (var codigo in usuario.CodigosDeRol())
                {
                    roles.AddClaim(new Claim(FirebaseClaims.Rol, codigo));
                }
            }
        }

        var resultado = principal.Clone();
        resultado.AddIdentity(roles);
        return resultado;
    }
}
