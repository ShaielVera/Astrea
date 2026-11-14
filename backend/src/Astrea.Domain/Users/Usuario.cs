using Astrea.Domain.Common;

namespace Astrea.Domain.Users;

/// <summary>
/// Identidad institucional vinculada a una cuenta de Firebase Authentication.
/// Los datos oficiales (nombre, apellido, DNI, legajo) los carga Bedelía; no se toman del cliente.
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

    public void Desactivar(DateTimeOffset ahora)
    {
        Activo = false;
        UpdatedAt = ahora;
    }

    private static string NormalizarEmail(string email)
    {
        var normalizado = email?.Trim().ToLowerInvariant() ?? string.Empty;
        var arroba = normalizado.IndexOf('@', StringComparison.Ordinal);
        if (arroba <= 0 || arroba != normalizado.LastIndexOf('@') || arroba == normalizado.Length - 1)
        {
            throw new DomainException("El email del usuario no es válido.");
        }

        return normalizado;
    }
}
