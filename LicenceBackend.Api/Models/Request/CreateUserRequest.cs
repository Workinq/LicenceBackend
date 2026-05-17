using System.ComponentModel.DataAnnotations;
using LicenceBackend.Core.Users;

namespace LicenceBackend.Api.Models.Request;

public sealed record CreateUserRequest
{
    [Required]
    [EmailAddress]
    [StringLength(256)]
    public required string Email { get; init; }

    [Required]
    [StringLength(PasswordPolicy.MaxLength, MinimumLength = PasswordPolicy.MinLength, ErrorMessage = "Password must be at least 12 characters.")]
    public required string Password { get; init; }

    [StringLength(200)]
    public string? DisplayName { get; init; }
}
