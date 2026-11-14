namespace Astrea.Application.Contracts.Pagination;

/// <summary>
/// Respuesta paginada estándar de la API:
/// { items, page, pageSize, totalItems, totalPages, hasPrevious, hasNext }.
/// </summary>
public sealed record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalItems)
{
    public int TotalPages => TotalItems == 0 ? 0 : (int)Math.Ceiling(TotalItems / (double)PageSize);

    public bool HasPrevious => Page > 1;

    public bool HasNext => Page < TotalPages;

    public static PagedResult<T> From(IReadOnlyList<T> items, PageRequest request, int totalItems) =>
        new(items, request.Page, request.PageSize, totalItems);
}
