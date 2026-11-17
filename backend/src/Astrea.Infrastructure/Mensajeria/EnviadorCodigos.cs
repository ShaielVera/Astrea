using System.Net;
using System.Net.Mail;
using Astrea.Application.Abstractions.Notifications;
using Astrea.Domain.Users;
using Microsoft.Extensions.Logging;

namespace Astrea.Infrastructure.Mensajeria;

/// <summary>
/// Envío de los códigos de verificación. El email sale por SMTP (por ejemplo, una cuenta de Gmail con contraseña de aplicación);
/// el usuario y la contraseña van en user-secrets (Mensajeria:Smtp:Usuario / Password), nunca en el repositorio.
/// </summary>
public sealed class ConfiguracionMensajeria
{
    public const string Seccion = "Mensajeria";

    public ConfiguracionSmtp Smtp { get; init; } = new();

    /// <summary>
    /// Solo para desarrollo y la presentación: si un canal no tiene proveedor, el código se escribe en el registro del servidor
    /// en lugar de mandarse. Nunca se activa en producción.
    /// </summary>
    public bool ModoDemostracion { get; init; }
}

public sealed class ConfiguracionSmtp
{
    public string Host { get; init; } = "smtp.gmail.com";

    public int Puerto { get; init; } = 587;

    public string? Usuario { get; init; }

    public string? Password { get; init; }

    /// <summary>Dirección que figura como remitente; si no se indica, la del usuario SMTP.</summary>
    public string? Remitente { get; init; }

    public string NombreRemitente { get; init; } = "Astrea · ISP N.º 20";

    public bool Configurado => !string.IsNullOrWhiteSpace(Host) && !string.IsNullOrWhiteSpace(Usuario) && !string.IsNullOrWhiteSpace(Password);
}

internal sealed class EnviadorCodigos(ConfiguracionMensajeria configuracion, ILogger<EnviadorCodigos> logger) : IEnviadorCodigos
{
    public async Task<EntregaCodigo> EnviarAsync(CanalContacto canal, string destino, string codigo, CancellationToken cancellationToken)
    {
        if (canal == CanalContacto.Email && configuracion.Smtp.Configurado)
        {
            await EnviarEmailAsync(destino, codigo, cancellationToken);
            return EntregaCodigo.Enviado;
        }

        // Todavía no hay proveedor de SMS: el celular solo se valida en modo demostración.
        if (!configuracion.ModoDemostracion)
        {
            return EntregaCodigo.NoConfigurado;
        }

        logger.LogWarning("MODO DEMOSTRACIÓN: código de verificación por {Canal} para ••{Final}: {Codigo}", canal, destino[^Math.Min(4, destino.Length)..], codigo);
        return EntregaCodigo.Simulado;
    }

    private async Task EnviarEmailAsync(string destino, string codigo, CancellationToken cancellationToken)
    {
        var smtp = configuracion.Smtp;
        using var mensaje = new MailMessage
        {
            From = new MailAddress(smtp.Remitente ?? smtp.Usuario!, smtp.NombreRemitente),
            Subject = $"Tu código de Astrea: {codigo}",
            Body =
                $"Tu código para validar este email en Astrea es: {codigo}\n\n" +
                "Vence en 10 minutos. Si no lo pediste vos, ignorá este mensaje.\n\n" +
                "Instituto Superior de Profesorado N.º 20",
        };
        mensaje.To.Add(destino);

        using var cliente = new SmtpClient(smtp.Host, smtp.Puerto)
        {
            EnableSsl = true,
            Credentials = new NetworkCredential(smtp.Usuario, smtp.Password),
            DeliveryMethod = SmtpDeliveryMethod.Network,
            Timeout = 20_000,
        };
        await cliente.SendMailAsync(mensaje, cancellationToken);
    }
}
