namespace LicenceBackend.Core.Common;

public sealed record PagedResult<T>(
    IReadOnlyList<T> Items,
    int Total
);
