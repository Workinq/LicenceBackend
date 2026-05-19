namespace LicenceBackend.Api.Models.Response;

public sealed record LicenceSeatsResponse(
    int MaxSeats,
    IReadOnlyList<LicenceSeatResponse> Live,
    PagedResponse<LicenceSeatHistoryEntryResponse> History
);
