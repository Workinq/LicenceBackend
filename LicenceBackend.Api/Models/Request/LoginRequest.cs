using System.ComponentModel.DataAnnotations;

namespace LicenceBackend.Api.Models.Request;

public sealed record LoginRequest(
    [Required]
    [EmailAddress]
    [StringLength(256)]
    string Email,
    [Required]
    [StringLength(256, MinimumLength = 1)]
    string Password
);
