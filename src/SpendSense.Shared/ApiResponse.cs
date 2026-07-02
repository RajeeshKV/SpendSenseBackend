namespace SpendSense.Shared;

public sealed record ApiResponse<T>(bool Success, string Message, T? Data, IReadOnlyList<string> Errors, string? CorrelationId)
{
    public static ApiResponse<T> Ok(T? data, string message = "Success", string? correlationId = null) => new(true, message, data, Array.Empty<string>(), correlationId);
    public static ApiResponse<T> Fail(string message, IEnumerable<string>? errors = null, string? correlationId = null) => new(false, message, default, errors?.ToArray() ?? Array.Empty<string>(), correlationId);
}

public sealed record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount)
{
    public int TotalPages => PageSize == 0 ? 0 : (int)Math.Ceiling((double)TotalCount / PageSize);
}
