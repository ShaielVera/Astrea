namespace Astrea.Domain.Users;

/// <summary>
/// Asignación de un rol a un usuario. Guarda quién la hizo y cuándo para auditoría.
/// <see cref="AsignadoPor"/> es null cuando la asignó el sistema (alta automática).
/// </summary>
public sealed class UsuarioRol
{
    private UsuarioRol()
    {
    }

    internal UsuarioRol(Guid usuarioId, Guid rolId, Guid? asignadoPor, DateTimeOffset asignadoAt)
    {
        UsuarioId = usuarioId;
        RolId = rolId;
        AsignadoPor = asignadoPor;
        AsignadoAt = asignadoAt;
    }

    public Guid UsuarioId { get; private set; }

    public Guid RolId { get; private set; }

    public Guid? AsignadoPor { get; private set; }

    public DateTimeOffset AsignadoAt { get; private set; }
}
