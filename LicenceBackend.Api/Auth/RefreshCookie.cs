namespace LicenceBackend.Api.Auth;

internal static class RefreshCookie
{
    public const string Name = "refresh_token";
    public const string Path = "/sessions";

    public static CookieOptions Build(DateTimeOffset expiresAt)
    {
        return new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Strict,
            Path = Path,
            Expires = expiresAt,
            IsEssential = true
        };
    }

    public static CookieOptions BuildExpiring()
    {
        return new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Strict,
            Path = Path,
            Expires = DateTimeOffset.UnixEpoch,
            IsEssential = true
        };
    }
}
