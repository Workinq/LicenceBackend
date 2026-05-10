namespace LicenceBackend.Api.Models.Request;

public sealed record UpdateLicenceIpAllowlistRequest(IReadOnlyList<string>? Cidrs, string? Reason);
