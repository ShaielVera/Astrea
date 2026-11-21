using Astrea.Domain.Common;

namespace Astrea.Domain.Users;

/// <summary>
/// Identidad institucional vinculada a una cuenta de Firebase Authentication.
/// El estudiante completa en "Mi perfil" su nombre, apellido, DNI, localidad y carreras (Secretaría también puede corregirlos).
/// El email y el celular de contacto se validan con un código y se renuevan cada año.
/// </summary>
public sealed class Usuario : Entity
{
    private readonly List<UsuarioRol> _roles = [];

    private Usuario()
    {
    }

    private Usuario(Guid id, string firebaseUid, string email, DateTimeOffset ahora)
        : base(id)
    {
        FirebaseUid = firebaseUid;
        Email = email;
        Activo = true;
        CreatedAt = ahora;
        UpdatedAt = ahora;
    }

    public string FirebaseUid { get; private set; } = null!;

    public string Email { get; private set; } = null!;

    public string? Nombre { get; private set; }

    public string? Apellido { get; private set; }

    public string? Dni { get; private set; }

    public string? Legajo { get; private set; }

    /// <summary>Ciudad o pueblo donde vive, tal como figura en <see cref="Localidades"/>.</summary>
    public string? Localidad { get; private set; }

    /// <summary>Carreras que cursa: son las que puede elegir al pedir un trámite.</summary>
    public List<Guid> CarrerasIds { get; private set; } = [];

    /// <summary>Email donde el instituto se comunica con el estudiante (puede ser distinto del de la cuenta).</summary>
    public string? EmailContacto { get; private set; }

    public DateTimeOffset? EmailContactoVerificadoEn { get; private set; }

    /// <summary>Celular solo con dígitos, con código de área (ej.: 3424123456).</summary>
    public string? Celular { get; private set; }

    public DateTimeOffset? CelularVerificadoEn { get; private set; }

    public bool Activo { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public IReadOnlyCollection<UsuarioRol> Roles => _roles.AsReadOnly();

    /// <summary>
    /// Da de alta un usuario a partir de una cuenta de Firebase verificada por el backend.
    /// Todo usuario nuevo comienza con el rol <see cref="Rol.Estudiante"/>.
    /// </summary>
    public static Usuario Registrar(Guid id, string firebaseUid, string email, DateTimeOffset ahora)
    {
        if (string.IsNullOrWhiteSpace(firebaseUid))
        {
            throw new DomainException("El usuario debe estar vinculado a una cuenta de Firebase.");
        }

        var usuario = new Usuario(id, firebaseUid.Trim(), NormalizarEmail(email), ahora);
        usuario.AsignarRol(Rol.Estudiante, asignadoPor: null, ahora);
        return usuario;
    }

    /// <summary>Asigna un rol. Es idempotente: si ya lo tiene, no hace nada.</summary>
    public void AsignarRol(Rol rol, Guid? asignadoPor, DateTimeOffset ahora)
    {
        ArgumentNullException.ThrowIfNull(rol);

        if (TieneRol(rol))
        {
            return;
        }

        _roles.Add(new UsuarioRol(Id, rol.Id, asignadoPor, ahora));
        UpdatedAt = ahora;
    }

    /// <summary>Quita un rol. El de estudiante no se quita: es la base de toda cuenta. Si no lo tiene, no hace nada.</summary>
    public void QuitarRol(Rol rol, DateTimeOffset ahora)
    {
        ArgumentNullException.ThrowIfNull(rol);
        if (rol.Id == Rol.Estudiante.Id)
        {
            throw new DomainException("El rol de estudiante no se puede quitar.");
        }

        if (_roles.RemoveAll(asignacion => asignacion.RolId == rol.Id) > 0)
        {
            UpdatedAt = ahora;
        }
    }

    /// <summary>"Pérez, Ana" si hay nombre y apellido; si no, el email.</summary>
    public string NombreParaMostrar => Apellido is not null && Nombre is not null ? $"{Apellido}, {Nombre}" : Email;

    public bool TieneRol(Rol rol) => _roles.Any(asignacion => asignacion.RolId == rol.Id);

    public IReadOnlyList<string> CodigosDeRol() =>
        _roles
            .Select(asignacion => Rol.PorId(asignacion.RolId)?.Codigo)
            .OfType<string>()
            .Order(StringComparer.Ordinal)
            .ToList();

    /// <summary>Sincroniza el email si cambió en Firebase.</summary>
    public void ActualizarEmail(string email, DateTimeOffset ahora)
    {
        var normalizado = NormalizarEmail(email);
        if (normalizado == Email)
        {
            return;
        }

        Email = normalizado;
        UpdatedAt = ahora;
    }

    /// <summary>Hay datos suficientes para emitir un documento oficial a su nombre.</summary>
    public bool TieneDatosOficiales => Nombre is not null && Apellido is not null && Dni is not null;

    public const int CarrerasMaximas = 5;

    public static readonly TimeSpan VigenciaContacto = TimeSpan.FromDays(365);

    /// <summary>
    /// El estudiante completa o corrige sus datos. La localidad se elige de <see cref="Localidades"/> y queda escrita
    /// como figura ahí; las carreras (que existan y estén activas lo controla quien llama) se guardan sin repetir.
    /// </summary>
    public void CompletarDatosPersonales(string nombre, string apellido, string dni, string? localidad, IEnumerable<Guid> carrerasIds, DateTimeOffset ahora)
    {
        var carreras = carrerasIds.Distinct().ToList();
        if (carreras.Count > CarrerasMaximas)
        {
            throw new DomainException($"Podés elegir hasta {CarrerasMaximas} carreras.");
        }

        Nombre = Texto.Requerido(nombre, "nombre", 100);
        Apellido = Texto.Requerido(apellido, "apellido", 100);
        Dni = NormalizarDni(dni);
        Localidad = string.IsNullOrWhiteSpace(localidad)
            ? null
            : Localidades.Buscar(localidad)?.Nombre ?? throw new DomainException("Elegí tu localidad de la lista.");
        CarrerasIds = carreras;
        UpdatedAt = ahora;
    }

    /// <summary>Cambia el email de contacto. Si es otro, hay que volver a validarlo.</summary>
    public void CambiarEmailContacto(string? email, DateTimeOffset ahora)
    {
        var normalizado = string.IsNullOrWhiteSpace(email) ? null : NormalizarEmail(email, "El email de contacto no es válido.");
        if (normalizado == EmailContacto)
        {
            return;
        }

        EmailContacto = normalizado;
        EmailContactoVerificadoEn = null;
        UpdatedAt = ahora;
    }

    /// <summary>Cambia el celular. Si es otro, hay que volver a validarlo.</summary>
    public void CambiarCelular(string? celular, DateTimeOffset ahora)
    {
        var normalizado = string.IsNullOrWhiteSpace(celular) ? null : NormalizarCelular(celular);
        if (normalizado == Celular)
        {
            return;
        }

        Celular = normalizado;
        CelularVerificadoEn = null;
        UpdatedAt = ahora;
    }

    /// <summary>Registra la validación con el código. El dato tiene que ser el actual (pudo cambiar mientras tanto).</summary>
    public void MarcarContactoVerificado(CanalContacto canal, string destino, DateTimeOffset ahora)
    {
        if (destino != ContactoDe(canal))
        {
            throw new DomainException("El dato cambió después de pedir el código: pedí uno nuevo.");
        }

        if (canal == CanalContacto.Email)
        {
            EmailContactoVerificadoEn = ahora;
        }
        else
        {
            CelularVerificadoEn = ahora;
        }

        UpdatedAt = ahora;
    }

    public string? ContactoDe(CanalContacto canal) => canal == CanalContacto.Email ? EmailContacto : Celular;

    public DateTimeOffset? ContactoVerificadoEn(CanalContacto canal) => canal == CanalContacto.Email ? EmailContactoVerificadoEn : CelularVerificadoEn;

    /// <summary>Validado hace menos de un año: después se pide revalidar por si cambió.</summary>
    public bool ContactoVigente(CanalContacto canal, DateTimeOffset ahora) =>
        ContactoVerificadoEn(canal) is { } verificado && ahora - verificado < VigenciaContacto;

    /// <summary>
    /// Secretaría corrige los datos que van en las constancias. El DNI se guarda solo con dígitos
    /// ("30.123.456" → "30123456") para poder compararlo y mostrarlo enmascarado.
    /// </summary>
    public void ActualizarDatosOficiales(string nombre, string apellido, string dni, string? legajo, DateTimeOffset ahora)
    {
        Nombre = Texto.Requerido(nombre, "nombre", 100);
        Apellido = Texto.Requerido(apellido, "apellido", 100);
        Dni = NormalizarDni(dni);
        Legajo = Texto.Opcional(legajo, "legajo", 30);
        UpdatedAt = ahora;
    }

    public void Desactivar(DateTimeOffset ahora)
    {
        Activo = false;
        UpdatedAt = ahora;
    }

    /// <summary>Solo dígitos ("30.123.456" → "30123456") para poder compararlo y mostrarlo enmascarado.</summary>
    public static string NormalizarDni(string? dni)
    {
        var soloDigitos = new string((dni ?? string.Empty).Where(c => !char.IsWhiteSpace(c) && c != '.' && c != '-').ToArray());
        if (soloDigitos.Length is < 7 or > 9 || !soloDigitos.All(char.IsAsciiDigit))
        {
            throw new DomainException("El DNI debe tener entre 7 y 9 dígitos.");
        }

        return soloDigitos;
    }

    /// <summary>
    /// Celular argentino con código de área, guardado solo con dígitos (10): acepta "+54 9 342 412-3456",
    /// "0342 15 412-3456" o "342 4123456" y los guarda como "3424123456".
    /// </summary>
    public static string NormalizarCelular(string celular)
    {
        var digitos = new string(celular.Where(char.IsAsciiDigit).ToArray());
        if (digitos.StartsWith("549", StringComparison.Ordinal))
        {
            digitos = digitos[3..];
        }
        else if (digitos.StartsWith("54", StringComparison.Ordinal) && digitos.Length == 12)
        {
            digitos = digitos[2..];
        }

        digitos = digitos.TrimStart('0');

        // "342 15 4123456": el 15 que va después del código de área no es parte del número.
        foreach (var largoArea in new[] { 2, 3, 4 })
        {
            if (digitos.Length == 12 && digitos.Substring(largoArea, 2) == "15")
            {
                digitos = digitos[..largoArea] + digitos[(largoArea + 2)..];
                break;
            }
        }

        if (digitos.Length != 10)
        {
            throw new DomainException("El celular tiene que tener código de área y número: 10 dígitos en total (ej.: 342 4123456).");
        }

        return digitos;
    }

    private static string NormalizarEmail(string email, string mensaje = "El email del usuario no es válido.")
    {
        var normalizado = email?.Trim().ToLowerInvariant() ?? string.Empty;
        var arroba = normalizado.IndexOf('@', StringComparison.Ordinal);
        if (arroba <= 0 || arroba != normalizado.LastIndexOf('@') || arroba == normalizado.Length - 1
            || normalizado.Length > 320 || normalizado.Any(char.IsWhiteSpace) || !normalizado[(arroba + 1)..].Contains('.', StringComparison.Ordinal))
        {
            throw new DomainException(mensaje);
        }

        return normalizado;
    }
}
