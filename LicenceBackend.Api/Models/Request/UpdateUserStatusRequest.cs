using System.ComponentModel.DataAnnotations;

namespace LicenceBackend.Api.Models.Request;

public sealed record UpdateUserStatusRequest(
    [Required]
    [RegularExpression("^(active|suspended)$", ErrorMessage = "Status must be 'active' or 'suspended'.")]
    string Status,
    [StringLength(500)] string? Reason
);
