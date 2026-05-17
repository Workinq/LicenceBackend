using System.ComponentModel.DataAnnotations;

namespace LicenceBackend.Api.Models.Request;

public sealed record UpdateProfileRequest(
    [StringLength(200)] string? DisplayName
);
