namespace Astrea.Application.Abstractions.Persistence;

/// <summary>
/// Otra persona modificó el mismo registro entre que se leyó y se guardó (concurrencia optimista).
/// La API la traduce a 409: el cliente debe recargar y volver a intentar.
/// </summary>
public sealed class ConflictoDeConcurrenciaException(Exception inner)
    : Exception("Otra persona modificó este registro al mismo tiempo. Recargá y volvé a intentar.", inner);
