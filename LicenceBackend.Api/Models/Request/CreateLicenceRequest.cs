using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace LicenceBackend.Api.Models.Request;

public sealed record CreateLicenceRequest(
    [property: JsonRequired][Required] Guid ProductId,
    Guid? UserId,
    [EmailAddress][StringLength(256)] string? Email,
    DateTimeOffset? ExpiresAt,
    string? Notes,
    IReadOnlyList<string>? IpAllowlist,
    int? MaxSeats = null
);
