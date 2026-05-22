using System.ComponentModel.DataAnnotations;

namespace LicenceBackend.Infrastructure.Options;

public sealed class StripeOptions
{
    public const string SectionName = "Stripe";

    [Required] public string SecretKey { get; init; } = string.Empty;
    [Required] public string WebhookSigningSecret { get; init; } = string.Empty;
    [Required] public string PublishableKey { get; init; } = string.Empty;
}
