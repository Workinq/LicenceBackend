using System.ComponentModel.DataAnnotations;

namespace LicenceBackend.Api.Models.Request;

public sealed record UpdateLicenceStatusRequest(
    [Required]
    [RegularExpression("^(active|suspended|revoked)$", ErrorMessage = "Status must be 'active', 'suspended', or 'revoked'.")]
    string Status,
    [StringLength(500)] string? Reason
);
