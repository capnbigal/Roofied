namespace Roofied.Application.Common;

public sealed record PagedResult<T>
{
    public IReadOnlyList<T> Items { get; init; } = Array.Empty<T>();
    public int Page { get; init; }
    public int PageSize { get; init; }
    public int TotalCount { get; init; }
    public int TotalPages => PageSize <= 0 ? 0 : (int)Math.Ceiling(TotalCount / (double)PageSize);
    public bool HasPrevious => Page > 1;
    public bool HasNext => Page < TotalPages;

    public static PagedResult<T> Create(IReadOnlyList<T> items, int page, int pageSize, int totalCount) =>
        new() { Items = items, Page = page, PageSize = pageSize, TotalCount = totalCount };
}

/// <summary>Lightweight operation result carrying success/failure and a message.</summary>
public sealed record OperationResult
{
    public bool Succeeded { get; init; }
    public string? Error { get; init; }
    public static OperationResult Success() => new() { Succeeded = true };
    public static OperationResult Fail(string error) => new() { Succeeded = false, Error = error };
}

public sealed record OperationResult<T>
{
    public bool Succeeded { get; init; }
    public string? Error { get; init; }
    public T? Value { get; init; }
    public static OperationResult<T> Success(T value) => new() { Succeeded = true, Value = value };
    public static OperationResult<T> Fail(string error) => new() { Succeeded = false, Error = error };
}
