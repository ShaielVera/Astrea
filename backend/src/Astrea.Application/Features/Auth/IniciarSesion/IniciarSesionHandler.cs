using Astrea.Application.Abstractions.Clock;
using Astrea.Application.Abstractions.Identity;
using Astrea.Application.Abstractions.Persistence;
using Astrea.Application.Contracts.Results;
using Astrea.Domain.Users;

namespace Astrea.Application.Features.Auth.IniciarSesion;

/// <summary>
/// Vincula la cuenta de Firebase autenticada con un usuario institucional.
/// La primera vez lo da de alta como Estudiante; las siguientes sincroniza el email.
/// Es idempotente: se puede llamar en cada inicio de sesión.
/// </summary>
public sealed class IniciarSesionHandler(
    ICurrentUser currentUser,
    IUserRepository usuarios,
    IUnitOfWork unitOfWork,
    IClock clock,
    ConfiguracionIdentidad configuracion)
{
    public async Task<Result<PerfilDto>> HandleAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(currentUser.FirebaseUid))
        {
            return ErroresAuth.SinIdentidad;
        }

        if (string.IsNullOrWhiteSpace(currentUser.Email))
        {
            return ErroresAuth.SinEmail;
        }

        try
        {
            return await SincronizarAsync(currentUser.FirebaseUid, currentUser.Email, cancellationToken);
        }
        catch (RegistroDuplicadoException)
        {
            // Otro inicio de sesión simultáneo del mismo usuario lo dio de alta (o le asignó el rol) primero:
            // se descarta lo pendiente y se repite sobre lo que ya quedó guardado.
            unitOfWork.DescartarCambios();
            return await SincronizarAsync(currentUser.FirebaseUid, currentUser.Email, cancellationToken);
        }
    }

    private async Task<Result<PerfilDto>> SincronizarAsync(string firebaseUid, string email, CancellationToken cancellationToken)
    {
        var ahora = clock.UtcNow;
        var usuario = await usuarios.ObtenerPorFirebaseUidAsync(firebaseUid, cancellationToken);

        if (usuario is null)
        {
            usuario = Usuario.Registrar(Guid.CreateVersion7(), firebaseUid, email, ahora);
            usuarios.Agregar(usuario);
        }
        else
        {
            usuario.ActualizarEmail(email, ahora);
        }

        if (!usuario.Activo)
        {
            return ErroresAuth.CuentaDesactivada;
        }

        usuario.RegistrarIngreso(ahora);

        // Sin email verificado cualquiera podría registrarse con el email de otra persona.
        if (currentUser.EmailVerificado && configuracion.EsAdministradorInicial(usuario.Email))
        {
            usuario.AsignarRol(Rol.Administrador, asignadoPor: null, ahora);
        }

        await unitOfWork.GuardarCambiosAsync(cancellationToken);

        return PerfilDto.Desde(usuario);
    }
}
