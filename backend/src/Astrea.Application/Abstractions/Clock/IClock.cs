namespace Astrea.Application.Abstractions.Clock;

/// <summary>
/// Puerto de salida para obtener la hora actual. Permite controlar el tiempo en los tests.
/// </summary>
public interface IClock
{
    DateTimeOffset UtcNow { get; }
}
