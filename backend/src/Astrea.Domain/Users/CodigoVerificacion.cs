using System.Security.Cryptography;
using System.Text;
using Astrea.Domain.Common;

namespace Astrea.Domain.Users;

/// <summary>Por dónde se valida un dato de contacto.</summary>
public enum CanalContacto
{
    Email = 1,
    Celular = 2,
}

/// <summary>Cómo terminó un intento de validar un código.</summary>
public enum ResultadoCodigo
{
    Correcto,
    Incorrecto,
    Vencido,
    SinIntentos,
}

/// <summary>
/// Código de 6 dígitos que se manda al email o al celular para comprobar que son del estudiante. Se guarda solo su hash
/// (nunca el código), vence a los 10 minutos y admite 5 intentos: después hay que pedir otro.
/// </summary>
public sealed class CodigoVerificacion : Entity
{
    public const int Digitos = 6;
    public const int IntentosMaximos = 5;
    public static readonly TimeSpan Vigencia = TimeSpan.FromMinutes(10);

    /// <summary>Espera mínima entre dos códigos del mismo canal: evita que se usen para mandar mensajes en masa.</summary>
    public static readonly TimeSpan EsperaEntreEnvios = TimeSpan.FromSeconds(60);

    private CodigoVerificacion()
    {
    }

    private CodigoVerificacion(Guid id, Guid usuarioId, CanalContacto canal, string destino, string hash, DateTimeOffset ahora)
        : base(id)
    {
        UsuarioId = usuarioId;
        Canal = canal;
        Destino = destino;
        Hash = hash;
        CreadoEn = ahora;
        VenceEn = ahora + Vigencia;
    }

    public Guid UsuarioId { get; private set; }

    public CanalContacto Canal { get; private set; }

    /// <summary>El email o celular al que se mandó: si el estudiante lo cambia, el código ya no sirve.</summary>
    public string Destino { get; private set; } = null!;

    public string Hash { get; private set; } = null!;

    public int Intentos { get; private set; }

    public DateTimeOffset CreadoEn { get; private set; }

    public DateTimeOffset VenceEn { get; private set; }

    public DateTimeOffset? UsadoEn { get; private set; }

    /// <summary>Crea el código para <paramref name="destino"/>; devuelve también el código en claro para enviarlo (no se guarda).</summary>
    public static (CodigoVerificacion Codigo, string EnClaro) Generar(Guid usuarioId, CanalContacto canal, string destino, DateTimeOffset ahora)
    {
        if (string.IsNullOrWhiteSpace(destino))
        {
            throw new DomainException("Primero guardá el dato que querés validar.");
        }

        var id = Guid.CreateVersion7();
        var enClaro = RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6", System.Globalization.CultureInfo.InvariantCulture);
        return (new CodigoVerificacion(id, usuarioId, canal, destino, Calcular(id, enClaro), ahora), enClaro);
    }

    /// <summary>Compara el código ingresado (en tiempo constante). Cada intento fallido descuenta uno.</summary>
    public ResultadoCodigo Validar(string? ingresado, DateTimeOffset ahora)
    {
        if (UsadoEn is not null || Intentos >= IntentosMaximos)
        {
            return ResultadoCodigo.SinIntentos;
        }

        if (ahora >= VenceEn)
        {
            return ResultadoCodigo.Vencido;
        }

        var limpio = new string((ingresado ?? string.Empty).Where(char.IsAsciiDigit).ToArray());
        var correcto = limpio.Length == Digitos
            && CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(Calcular(Id, limpio)), Encoding.ASCII.GetBytes(Hash));
        if (!correcto)
        {
            Intentos++;
            return Intentos >= IntentosMaximos ? ResultadoCodigo.SinIntentos : ResultadoCodigo.Incorrecto;
        }

        UsadoEn = ahora;
        return ResultadoCodigo.Correcto;
    }

    public int IntentosRestantes => Math.Max(0, IntentosMaximos - Intentos);

    private static string Calcular(Guid id, string codigo) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes($"{id:N}:{codigo}")));
}
