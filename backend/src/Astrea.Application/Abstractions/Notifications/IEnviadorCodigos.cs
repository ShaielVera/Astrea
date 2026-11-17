using Astrea.Domain.Users;

namespace Astrea.Application.Abstractions.Notifications;

/// <summary>Cómo se entregó un código de verificación.</summary>
public enum EntregaCodigo
{
    /// <summary>Se mandó de verdad (email por SMTP, SMS por un proveedor).</summary>
    Enviado,

    /// <summary>Modo demostración (solo desarrollo): el código quedó en el registro del servidor, no se mandó nada.</summary>
    Simulado,

    /// <summary>El canal no tiene un proveedor configurado: no se puede validar por ahora.</summary>
    NoConfigurado,
}

/// <summary>Manda el código de verificación al email o celular del estudiante.</summary>
public interface IEnviadorCodigos
{
    Task<EntregaCodigo> EnviarAsync(CanalContacto canal, string destino, string codigo, CancellationToken cancellationToken);
}
