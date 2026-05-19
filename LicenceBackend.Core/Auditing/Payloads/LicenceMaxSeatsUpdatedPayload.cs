namespace LicenceBackend.Core.Auditing.Payloads;

public sealed record LicenceMaxSeatsUpdatedPayload(
    int PreviousMaxSeats,
    int NewMaxSeats
);
