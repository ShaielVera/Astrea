namespace Astrea.Application.Abstractions.Persistence;

public interface IUnitOfWork
{
    /// <exception cref="ConflictoDeConcurrenciaException">Otro cambio pisó al registro desde que se leyó.</exception>
    /// <exception cref="RegistroDuplicadoException">Otro pedido creó al mismo tiempo un registro con la misma clave única.</exception>
    Task GuardarCambiosAsync(CancellationToken cancellationToken);

    /// <summary>Descarta los cambios pendientes (por ejemplo, para reintentar tras un <see cref="RegistroDuplicadoException"/>).</summary>
    void DescartarCambios();
}

/// <summary>
/// Se violó una restricción única porque otro pedido simultáneo guardó lo mismo primero
/// (ej. dos inicios de sesión a la vez de un usuario nuevo). La API la traduce a 409.
/// </summary>
public sealed class RegistroDuplicadoException(Exception inner)
    : Exception("Otro pedido registró lo mismo al mismo tiempo. Volvé a intentar.", inner);
