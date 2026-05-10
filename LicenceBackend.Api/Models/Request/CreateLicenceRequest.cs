using System.ComponentModel.DataAnnotations;

namespace LicenceBackend.Api.Models.Request;

public sealed record CreateLicenceRequest(
    [Required] Guid ProductId,
    Guid? UserId,
    [EmailAddress][StringLength(256)] string? Email,
    DateTimeOffset? ExpiresAt,
    string? Notes
);
