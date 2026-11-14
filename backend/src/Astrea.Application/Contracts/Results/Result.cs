namespace Astrea.Application.Contracts.Results;

public enum TipoError
{
    Validacion,
    NoAutenticado,
    Prohibido,
    NoEncontrado,
    Conflicto,
}

/// <summary>
/// Error esperado de un caso de uso. La API lo traduce a Problem Details con el código HTTP que corresponda.
/// </summary>
public sealed record Error(TipoError Tipo, string Codigo, string Mensaje)
{
    public static Error Validacion(string codigo, string mensaje) => new(TipoError.Validacion, codigo, mensaje);

    public static Error NoAutenticado(string codigo, string mensaje) => new(TipoError.NoAutenticado, codigo, mensaje);

    public static Error Prohibido(string codigo, string mensaje) => new(TipoError.Prohibido, codigo, mensaje);

    public static Error NoEncontrado(string codigo, string mensaje) => new(TipoError.NoEncontrado, codigo, mensaje);

    public static Error Conflicto(string codigo, string mensaje) => new(TipoError.Conflicto, codigo, mensaje);
}

/// <summary>
/// Resultado de un caso de uso: un valor o un <see cref="Error"/> esperado.
/// Las excepciones quedan reservadas para fallas inesperadas.
/// </summary>
public sealed class Result<T>
{
    private readonly T? _valor;

    private Result(T valor)
    {
        _valor = valor;
    }

    private Result(Error error)
    {
        Error = error;
    }

    public Error? Error { get; }

    public bool EsExitoso => Error is null;

    public T Valor => EsExitoso
        ? _valor!
        : throw new InvalidOperationException("No se puede leer el valor de un resultado fallido.");

    /// <summary>Para valores de tipo interfaz, que no admiten la conversión implícita.</summary>
    public static Result<T> Exito(T valor) => new(valor);

    public static implicit operator Result<T>(T valor) => new(valor);

    public static implicit operator Result<T>(Error error) => new(error);
}
