namespace Astrea.Domain.Common;

/// <summary>
/// Se lanza cuando una operación viola una regla de negocio del dominio.
/// </summary>
public class DomainException(string message) : Exception(message);
