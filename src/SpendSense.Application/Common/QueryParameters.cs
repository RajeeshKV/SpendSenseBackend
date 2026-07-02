namespace SpendSense.Application.Common;

public sealed record QueryParameters(int Page = 1, int PageSize = 25, string? SortBy = null, string? SortDirection = null)
{
    public int SafePage => Page < 1 ? 1 : Page;
    public int SafePageSize => PageSize is < 1 or > 100 ? 25 : PageSize;
}
