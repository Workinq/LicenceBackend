namespace LicenceBackend.Api.Models.Response;

public sealed record PagedResponse<T>(
    IReadOnlyList<T> Items,
    int Total,
    int Limit,
    int Offset
);
