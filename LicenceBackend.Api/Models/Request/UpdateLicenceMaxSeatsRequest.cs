using System.ComponentModel.DataAnnotations;

namespace LicenceBackend.Api.Models.Request;

public sealed record UpdateLicenceMaxSeatsRequest(
    [Range(1, 1000)] int MaxSeats,
    [StringLength(500)] string? Reason
);
