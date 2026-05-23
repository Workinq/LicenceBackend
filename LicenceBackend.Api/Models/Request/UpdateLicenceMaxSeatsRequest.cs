using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace LicenceBackend.Api.Models.Request;

public sealed record UpdateLicenceMaxSeatsRequest(
    [property: JsonRequired][Range(1, 1000)] int MaxSeats,
    [StringLength(500)] string? Reason
);
