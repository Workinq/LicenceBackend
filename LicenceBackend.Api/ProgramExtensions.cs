using System.Threading.RateLimiting;
using LicenceBackend.Api.RateLimiting;
using LicenceBackend.Infrastructure.Crypto;
using LicenceBackend.Infrastructure.Options;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using SessionOptions = LicenceBackend.Infrastructure.Options.SessionOptions;

namespace LicenceBackend.Api;

internal static class ProgramExtensions
{
    public static IServiceCollection AddLicenceBackendJwtBearer(this IServiceCollection services)
    {
        services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer();

        services
            .AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<SessionSigningKeySet, IOptions<SessionOptions>>((options, signingKeySet, sessionOpts) =>
            {
                options.MapInboundClaims = false;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = sessionOpts.Value.Issuer,
                    ValidateAudience = true,
                    ValidAudience = sessionOpts.Value.Audience,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKeys = signingKeySet.AllSecurityKeys.ToArray(),
                    ValidAlgorithms = [SecurityAlgorithms.EcdsaSha256],
                    RequireExpirationTime = true,
                    RequireSignedTokens = true,
                    RoleClaimType = "role",
                    NameClaimType = "sub",
                    ClockSkew = TimeSpan.FromSeconds(30)
                };

                options.Events = new JwtBearerEvents
                {
                    OnMessageReceived = context =>
                    {
                        var path = context.HttpContext.Request.Path;
                        if (path.StartsWithSegments("/openapi") || path.StartsWithSegments("/scalar"))
                        {
                            context.NoResult();
                        }
                        return Task.CompletedTask;
                    }
                };
            });

        return services;
    }

    public static IServiceCollection AddLicenceBackendRateLimiter(this IServiceCollection services, RateLimitingOptions rateLimitingOptions)
    {
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.OnRejected = async (context, cancellationToken) =>
            {
                TimeSpan? retryAfter = null;
                if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var metadata))
                {
                    retryAfter = metadata;
                }
                await RateLimitRejection.WriteAsync(context.HttpContext, retryAfter, cancellationToken);
            };

            options.AddPolicy(RateLimiterPolicyNames.Refresh, httpContext =>
            {
                var key = ClientIpKey(httpContext);
                return RateLimitPartition.GetSlidingWindowLimiter(key, _ => BuildSlidingWindow(rateLimitingOptions.Refresh));
            });

            options.AddPolicy(RateLimiterPolicyNames.VerifyPublicKey, httpContext =>
            {
                var key = ClientIpKey(httpContext);
                return RateLimitPartition.GetSlidingWindowLimiter(key, _ => BuildSlidingWindow(rateLimitingOptions.VerifyPublicKey));
            });

            options.AddPolicy(RateLimiterPolicyNames.Admin, httpContext =>
            {
                var sub = httpContext.User.FindFirst("sub")?.Value;
                var key = !string.IsNullOrWhiteSpace(sub) ? $"user:{sub}" : ClientIpKey(httpContext);
                return RateLimitPartition.GetSlidingWindowLimiter(key, _ => BuildSlidingWindow(rateLimitingOptions.Admin));
            });

            options.AddPolicy(RateLimiterPolicyNames.CheckoutHeartbeat, httpContext =>
            {
                var seatId = httpContext.Request.RouteValues["seatId"]?.ToString() ?? string.Empty;
                var key = $"seat:{seatId}";
                return RateLimitPartition.GetSlidingWindowLimiter(key, _ => BuildSlidingWindow(rateLimitingOptions.Heartbeat));
            });

            options.AddPolicy(RateLimiterPolicyNames.CheckoutCheckin, httpContext =>
            {
                var seatId = httpContext.Request.RouteValues["seatId"]?.ToString() ?? string.Empty;
                var key = $"seat:{seatId}";
                return RateLimitPartition.GetSlidingWindowLimiter(key, _ => BuildSlidingWindow(rateLimitingOptions.Checkin));
            });

            options.AddPolicy(RateLimiterPolicyNames.StripeWebhook, httpContext =>
            {
                var key = ClientIpKey(httpContext);
                return RateLimitPartition.GetSlidingWindowLimiter(key, _ => BuildSlidingWindow(rateLimitingOptions.StripeWebhook));
            });
        });

        return services;
    }

    private static string ClientIpKey(HttpContext context)
    {
        var ip = context.Connection.RemoteIpAddress;
        return ip is null ? "ip:unknown" : $"ip:{ip}";
    }

    private static SlidingWindowRateLimiterOptions BuildSlidingWindow(RateLimitPolicyOptions policy)
    {
        return new SlidingWindowRateLimiterOptions
        {
            PermitLimit = policy.PermitLimit,
            Window = TimeSpan.FromSeconds(policy.WindowSeconds),
            SegmentsPerWindow = 6,
            QueueLimit = 0,
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
            AutoReplenishment = true
        };
    }
}
