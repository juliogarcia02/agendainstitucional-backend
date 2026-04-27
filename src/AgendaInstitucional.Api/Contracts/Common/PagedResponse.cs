namespace AgendaInstitucional.Api.Contracts.Common;

public class PagedResponse<T>
{
    public IReadOnlyCollection<T> Items { get; set; } = [];

    public int Page { get; set; }

    public int PageSize { get; set; }

    public int TotalRecords { get; set; }

    public int TotalPages => PageSize <= 0 ? 0 : (int)Math.Ceiling((double)TotalRecords / PageSize);
}