using System.Net;
using System.Threading.RateLimiting;
using LicenceBackend.Api.RateLimiting;
using LicenceBackend.Infrastructure;
using LicenceBackend.Infrastructure.Crypto;
using LicenceBackend.Infrastructure.Options;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Scalar.AspNetCore;
using Serilog;
using IPNetwork = Microsoft.AspNetCore.HttpOverrides.IPNetwork;
using SessionOptions = LicenceBackend.Infrastructure.Options.SessionOptions;

Log.Logger = new LoggerConfiguration()
             .WriteTo.Console()
             .CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    builder.Host.UseSerilog((context, services, loggerConfiguration) =>
        loggerConfiguration
            .ReadFrom.Configuration(context.Configuration)
            .ReadFrom.Services(services)
            .Enrich.FromLogContext());

    builder.Services.AddControllers(options => { options.Filters.Add(new ProducesAttribute("application/json")); });
    builder.Services.AddOpenApi(options =>
    {
        options.AddDocumentTransformer((document, _, _) =>
           {
               document.Info.Title   = "LicenceBackend API";
               document.Info.Version = "1.0.0";
               return Task.CompletedTask;
           });
    });
    builder.Services.AddProblemDetails();
    builder.Services.AddLicenceBackendInfrastructure(builder.Configuration);

    builder.Services
           .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
           .AddJwtBearer();

    builder.Services
           .AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
           .Configure<SessionSigningKeySet, IOptions<SessionOptions>>((options, signingKeySet, sessionOpts) =>
              {
                  options.MapInboundClaims = false;
                  options.TokenValidationParameters = new TokenValidationParameters
                  {
                      ValidateIssuer           = true,
                      ValidIssuer              = sessionOpts.Value.Issuer,
                      ValidateAudience         = true,
                      ValidAudience            = sessionOpts.Value.Audience,
                      ValidateLifetime         = true,
                      ValidateIssuerSigningKey = true,
                      IssuerSigningKeys        = signingKeySet.AllSecurityKeys.ToArray(),
                      ValidAlgorithms          = [SecurityAlgorithms.EcdsaSha256],
                      RequireExpirationTime    = true,
                      RequireSignedTokens      = true,
                      RoleClaimType            = "role",
                      NameClaimType            = "sub",
                      ClockSkew                = TimeSpan.FromSeconds(30)
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

    builder.Services.AddAuthorization();

    builder.Services.Configure<ForwardedHeadersOptions>(options =>
    {
        options.ForwardedHeaders = ForwardedHeaders.XForwardedFor;
        options.KnownNetworks.Clear();
        options.KnownProxies.Clear();
        options.KnownNetworks.Add(new IPNetwork(IPAddress.Loopback,     8));
        options.KnownNetworks.Add(new IPNetwork(IPAddress.IPv6Loopback, 128));
    });

    var rateLimitingOptions = builder.Configuration
                                     .GetSection(RateLimitingOptions.SectionName)
                                     .Get<RateLimitingOptions>() ?? new RateLimitingOptions();

    if (rateLimitingOptions.Enabled)
        builder.Services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.OnRejected = async (context, cancellationToken) =>
             {
                 TimeSpan? retryAfter = null;
                 if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var metadata))
                     retryAfter = metadata;
                 await RateLimitRejection.WriteAsync(context.HttpContext, retryAfter, cancellationToken);
             };

            options.AddPolicy(RateLimiterPolicyNames.Refresh, httpContext =>
            {
                var key = ClientIpKey(httpContext);
                return RateLimitPartition.GetSlidingWindowLimiter(
                    key,
                _ => BuildSlidingWindow(rateLimitingOptions.Refresh)
                );
            });

            options.AddPolicy(RateLimiterPolicyNames.VerifyPublicKey, httpContext =>
                                                                      {
                                                                          var key = ClientIpKey(httpContext);
                                                                          return RateLimitPartition.GetSlidingWindowLimiter(
                                                                              key,
                                                                              _ => BuildSlidingWindow(
                                                                                  rateLimitingOptions.VerifyPublicKey));
                                                                      });

            options.AddPolicy(RateLimiterPolicyNames.Admin, httpContext =>
                                                            {
                                                                var sub = httpContext.User.FindFirst("sub")?.Value;
                                                                var key = !string.IsNullOrWhiteSpace(sub)
                                                                    ? $"user:{sub}"
                                                                    : ClientIpKey(httpContext);
                                                                return RateLimitPartition.GetSlidingWindowLimiter(
                                                                    key, _ => BuildSlidingWindow(rateLimitingOptions.Admin));
                                                            });
        });

    var app = builder.Build();

    app.UseForwardedHeaders();
    app.UseSerilogRequestLogging();
    app.UseStatusCodePages();

    if (app.Environment.IsDevelopment())
    {
        app.MapOpenApi();
        app.MapScalarApiReference();
    }

    app.UseAuthentication();
    app.UseAuthorization();

    if (rateLimitingOptions.Enabled) app.UseRateLimiter();

    app.MapControllers();

    app.Run();
}
catch (Exception ex) when (ex is not HostAbortedException)
{
    Log.Fatal(ex, "LicenceBackend.Api terminated unexpectedly");
    throw;
}
finally
{
    await Log.CloseAndFlushAsync();
}

public abstract partial class Program
{
    private static string ClientIpKey(HttpContext context)
    {
        var ip = context.Connection.RemoteIpAddress;
        return ip is null ? "ip:unknown" : $"ip:{ip}";
    }

    private static SlidingWindowRateLimiterOptions BuildSlidingWindow(RateLimitPolicyOptions policy)
    {
        return new SlidingWindowRateLimiterOptions
        {
            PermitLimit          = policy.PermitLimit,
            Window               = TimeSpan.FromSeconds(policy.WindowSeconds),
            SegmentsPerWindow    = 6,
            QueueLimit           = 0,
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
            AutoReplenishment    = true
        };
    }
}
