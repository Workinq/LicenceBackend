using System.ComponentModel.DataAnnotations;

namespace LicenceBackend.Api.Models.Request;

public sealed record CreateProductRequest(
    [Required]
    [RegularExpression("^[a-z0-9][a-z0-9-]*$", ErrorMessage = "Slug must be lowercase alphanumeric and hyphens only.")]
    string Slug,
    [Required]
    [StringLength(200, MinimumLength = 1)]
    string DisplayName
);
