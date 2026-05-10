using System.ComponentModel.DataAnnotations;

namespace LicenceBackend.Api.Models.Request;

public sealed record CreateUserRequest(
    [Required]
    [EmailAddress]
    [StringLength(256)]
    string Email,
    [Required]
    [StringLength(256, MinimumLength = 12, ErrorMessage = "Password must be at least 12 characters.")]
    string Password,
    [StringLength(200)] string? DisplayName,
    [Required]
    [RegularExpression("^(user|admin)$", ErrorMessage = "Role must be 'user' or 'admin'.")]
    string Role
);
