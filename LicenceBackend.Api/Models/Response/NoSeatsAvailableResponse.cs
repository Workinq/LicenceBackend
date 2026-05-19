namespace LicenceBackend.Api.Models.Response;

public sealed record NoSeatsAvailableResponse(
    string Error,
    int MaxSeats,
    int ActiveSeats,
    DateTimeOffset OldestExpiresAt
);
