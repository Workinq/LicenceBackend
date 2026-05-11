using System.ComponentModel.DataAnnotations;

namespace LicenceBackend.Api.Models.Request;

public sealed record RegenerateLicenceKeyRequest(
    [StringLength(500)] string? Reason
);
