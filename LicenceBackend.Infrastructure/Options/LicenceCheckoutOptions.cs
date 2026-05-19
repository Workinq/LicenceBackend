using System.ComponentModel.DataAnnotations;

namespace LicenceBackend.Infrastructure.Options;

public sealed class LicenceCheckoutOptions
{
    public const string SectionName = "LicenceCheckout";

    [Range(30, 86_400)] public int LeaseSeconds { get; init; } = 600;
    [Range(15, 86_400)] public int HeartbeatHintSeconds { get; init; } = 300;
    [Range(16, 256)] public int MinInstanceIdLength { get; init; } = 16;
    [Range(16, 512)] public int MaxInstanceIdLength { get; init; } = 128;
}
