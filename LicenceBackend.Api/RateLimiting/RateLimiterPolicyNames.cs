namespace LicenceBackend.Api.RateLimiting;

public static class RateLimiterPolicyNames
{
    public const string Refresh = "refresh";
    public const string VerifyPublicKey = "verify-public-key";
    public const string Admin = "admin";
    public const string CheckoutHeartbeat = "checkout-heartbeat";
    public const string CheckoutCheckin = "checkout-checkin";
    public const string StripeWebhook = "stripe-webhook";
}
