using System.ComponentModel.DataAnnotations;
using LicenceBackend.Core.Users;

namespace LicenceBackend.Api.Models.Request;

public sealed record ChangePasswordRequest
{
    [Required]
    [StringLength(PasswordPolicy.MaxLength)]
    public required string CurrentPassword { get; init; }

    [Required]
    [StringLength(PasswordPolicy.MaxLength, MinimumLength = PasswordPolicy.MinLength, ErrorMessage = "Password must be at least 12 characters.")]
    public required string NewPassword { get; init; }
}
