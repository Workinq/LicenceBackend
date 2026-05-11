using System.ComponentModel.DataAnnotations;

namespace LicenceBackend.Api.Models.Request;

public sealed record CreateProductRequest(
    [Required]
    [RegularExpression("^[a-z0-9][a-z0-9-]*$", ErrorMessage = "Slug must be lowercase alphanumeric and hyphens only.")]
    string Slug,
    [Required]
    [StringLength(200, MinimumLength = 1)]
    string DisplayName,
    [StringLength(4000)]
    string? Description,
    [StringLength(280)]
    string? Tagline,
    bool? IsPublic,
    [Range(0, 99999999.99)]
    decimal? Price,
    [RegularExpression("^[A-Z]{3}$", ErrorMessage = "Currency must be a 3-letter uppercase code.")]
    string? Currency,
    int? SortOrder
);
