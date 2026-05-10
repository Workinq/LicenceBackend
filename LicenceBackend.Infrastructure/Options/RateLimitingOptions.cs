using System.ComponentModel.DataAnnotations;

namespace LicenceBackend.Infrastructure.Options;

public sealed class RateLimitingOptions
{
    public const string SectionName = "RateLimiting";

    public bool Enabled { get; init; } = true;

    public RateLimitPolicyOptions Login           { get; init; } = new() { PermitLimit = 10, WindowSeconds  = 60 };
    public RateLimitPolicyOptions Refresh         { get; init; } = new() { PermitLimit = 30, WindowSeconds  = 60 };
    public RateLimitPolicyOptions Verify          { get; init; } = new() { PermitLimit = 60, WindowSeconds  = 60 };
    public RateLimitPolicyOptions VerifyPublicKey { get; init; } = new() { PermitLimit = 20, WindowSeconds  = 60 };
    public RateLimitPolicyOptions Admin           { get; init; } = new() { PermitLimit = 300, WindowSeconds = 60 };
}

public sealed class RateLimitPolicyOptions
{
    [Range(1, int.MaxValue)] public int PermitLimit { get; init; }

    [Range(1, 86_400)] public int WindowSeconds { get; init; }
}
