namespace Astrea.Application.Contracts.Pagination;

/// <summary>
/// Parámetros de paginación de la API: page empieza en 1 y pageSize tiene un máximo.
/// </summary>
public sealed record PageRequest
{
    public const int DefaultPageSize = 20;
    public const int MaxPageSize = 50;

    private PageRequest(int page, int pageSize)
    {
        Page = page;
        PageSize = pageSize;
    }

    public int Page { get; }

    public int PageSize { get; }

    public int Skip => (Page - 1) * PageSize;

    /// <summary>
    /// Normaliza los valores recibidos: valores ausentes o menores a 1 toman el valor por defecto
    /// y un pageSize mayor al máximo se recorta a <see cref="MaxPageSize"/>.
    /// </summary>
    public static PageRequest Create(int? page, int? pageSize)
    {
        var normalizedPage = page is null or < 1 ? 1 : page.Value;
        var normalizedSize = pageSize is null or < 1
            ? DefaultPageSize
            : Math.Min(pageSize.Value, MaxPageSize);

        return new PageRequest(normalizedPage, normalizedSize);
    }
}
