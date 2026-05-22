using Dapper;
using LicenceBackend.Core.Auditing;
using LicenceBackend.Core.Invoices;
using LicenceBackend.Core.Licences;
using LicenceBackend.Core.Orders;
using LicenceBackend.Core.Payments;
using LicenceBackend.Core.Products;
using LicenceBackend.Core.Sessions;
using LicenceBackend.Core.Users;
using LicenceBackend.Infrastructure.Crypto;
using LicenceBackend.Infrastructure.Hosting;
using LicenceBackend.Infrastructure.Options;
using LicenceBackend.Infrastructure.Payments;
using LicenceBackend.Infrastructure.Persistence;
using LicenceBackend.Infrastructure.RateLimiting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Npgsql;

namespace LicenceBackend.Infrastructure;

public static class ServiceCollectionExtensions
{
    public static void AddLicenceBackendInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        DefaultTypeMap.MatchNamesWithUnderscores = true;

        services.AddOptions<LicenceOptions>()
                .Bind(configuration.GetSection(LicenceOptions.SectionName))
                .Validate(o => o.Peppers.Count > 0, "Licence:Peppers must contain at least one entry.")
                .Validate(o => o.Peppers.All(p => p.Version > 0), "Licence:Peppers entries must have Version > 0.")
                .Validate(o => o.Peppers.All(p => !string.IsNullOrWhiteSpace(p.Path)), "Licence:Peppers entries must have a non-empty Path.")
                .Validate(o => o.Peppers.Select(p => p.Version).Distinct().Count() == o.Peppers.Count, "Licence:Peppers must not contain duplicate Version values.")
                .Validate(o => o.ActivePepperVersion > 0, "Licence:ActivePepperVersion must be > 0.")
                .Validate(o => o.Peppers.Any(p => p.Version == o.ActivePepperVersion), "Licence:ActivePepperVersion must match one of the configured Peppers.")
                .ValidateOnStart();

        services.AddOptions<SessionSigningOptions>()
                .Bind(configuration.GetSection(SessionSigningOptions.SectionName))
                .Validate(o => o.Keys.Count > 0, "SessionSigning:Keys must contain at least one entry.")
                .Validate(o => o.Keys.All(k => !string.IsNullOrWhiteSpace(k.Kid)), "SessionSigning:Keys entries must have a non-empty Kid.")
                .Validate(o => o.Keys.All(k => !string.IsNullOrWhiteSpace(k.PrivateKeyPath)), "SessionSigning:Keys entries must have a non-empty PrivateKeyPath.")
                .Validate(o => o.Keys.Select(k => k.Kid).Distinct().Count() == o.Keys.Count, "SessionSigning:Keys must not contain duplicate Kid values.")
                .Validate(o => !string.IsNullOrWhiteSpace(o.ActiveKid), "SessionSigning:ActiveKid is required.")
                .Validate(o => o.Keys.Any(k => k.Kid == o.ActiveKid), "SessionSigning:ActiveKid must match one of the configured Keys.")
                .ValidateOnStart();

        services.AddOptions<LicenceVerifySigningOptions>()
                .Bind(configuration.GetSection(LicenceVerifySigningOptions.SectionName))
                .Validate(o => o.Keys.Count > 0, "LicenceVerifySigning:Keys must contain at least one entry.")
                .Validate(o => o.Keys.All(k => !string.IsNullOrWhiteSpace(k.Kid)), "LicenceVerifySigning:Keys entries must have a non-empty Kid.")
                .Validate(o => o.Keys.All(k => !string.IsNullOrWhiteSpace(k.PrivateKeyPath)), "LicenceVerifySigning:Keys entries must have a non-empty PrivateKeyPath.")
                .Validate(o => o.Keys.Select(k => k.Kid).Distinct().Count() == o.Keys.Count, "LicenceVerifySigning:Keys must not contain duplicate Kid values.")
                .Validate(o => !string.IsNullOrWhiteSpace(o.ActiveKid), "LicenceVerifySigning:ActiveKid is required.")
                .Validate(o => o.Keys.Any(k => k.Kid == o.ActiveKid), "LicenceVerifySigning:ActiveKid must match one of the configured Keys.")
                .ValidateOnStart();

        services.AddOptions<SessionOptions>()
                .Bind(configuration.GetSection(SessionOptions.SectionName))
                .Validate(o => !string.IsNullOrWhiteSpace(o.Issuer), "Session:Issuer is required.")
                .Validate(o => !string.IsNullOrWhiteSpace(o.Audience), "Session:Audience is required.")
                .Validate(o => o.TtlSeconds > 0, "Session:TtlSeconds must be positive.")
                .Validate(o => o.RefreshTtlSeconds > 0, "Session:RefreshTtlSeconds must be positive.")
                .ValidateOnStart();

        services.AddOptions<RateLimitingOptions>()
                .Bind(configuration.GetSection(RateLimitingOptions.SectionName))
                .ValidateDataAnnotations()
                .ValidateOnStart();

        services.AddOptions<LicenceCheckoutOptions>()
                .Bind(configuration.GetSection(LicenceCheckoutOptions.SectionName))
                .ValidateDataAnnotations()
                .ValidateOnStart();

        services.AddOptions<ProductImageStorageOptions>()
                .Bind(configuration.GetSection(ProductImageStorageOptions.SectionName));

        services.AddOptions<ProductFileStorageOptions>()
                .Bind(configuration.GetSection(ProductFileStorageOptions.SectionName));

        services.AddOptions<InvoicingOptions>()
                .Bind(configuration.GetSection(InvoicingOptions.SectionName))
                .ValidateDataAnnotations()
                .ValidateOnStart();

        services.AddOptions<ProductContentImageStorageOptions>()
                .Bind(configuration.GetSection(ProductContentImageStorageOptions.SectionName));

        services.AddOptions<StripeOptions>()
                .Bind(configuration.GetSection(StripeOptions.SectionName))
                .ValidateDataAnnotations()
                .ValidateOnStart();

        var connectionString = configuration.GetConnectionString("Postgres") ?? throw new InvalidOperationException("ConnectionStrings:Postgres is required.");
        services.AddSingleton(NpgsqlDataSource.Create(connectionString));

        services.AddSingleton(TimeProvider.System);

        services.AddSingleton<SessionSigningKeySet>(sp =>
            {
                var options = sp.GetRequiredService<IOptions<SessionSigningOptions>>().Value;
                var keys = options.Keys.ToDictionary(k => k.Kid, k => EcdsaKeyLoader.LoadFromPemFile(k.PrivateKeyPath));
                return new SessionSigningKeySet(keys, options.ActiveKid);
            }
        );

        services.AddSingleton<LicenceVerifySigningKeySet>(sp =>
            {
                var options = sp.GetRequiredService<IOptions<LicenceVerifySigningOptions>>().Value;
                var keys = options.Keys.ToDictionary(k => k.Kid, k => EcdsaKeyLoader.LoadFromPemFile(k.PrivateKeyPath));
                return new LicenceVerifySigningKeySet(keys, options.ActiveKid);
            }
        );

        services.AddSingleton<HmacPepperSet>(sp =>
            {
                var options = sp.GetRequiredService<IOptions<LicenceOptions>>().Value;
                var peppers = options.Peppers.ToDictionary(p => p.Version, p => LoadPepper(p.Path));
                return new HmacPepperSet(peppers, options.ActivePepperVersion);
            }
        );

        services.AddSingleton<ILicenceKeyHasher, HmacLicenceKeyHasher>();
        services.AddSingleton<IHwidHasher, HmacHwidHasher>();
        services.AddSingleton<ISessionTokenIssuer, JwtSessionTokenIssuer>();
        services.AddSingleton<ILicenceVerificationSigner, JwtLicenceVerificationSigner>();
        services.AddSingleton<ILicenceKeyGenerator, LicenceKeyGenerator>();
        services.AddSingleton<IPasswordHasher, Argon2IdPasswordHasher>();
        services.AddSingleton<ILicenceRepository, LicenceRepository>();
        services.AddSingleton<ILicenceMemberRepository, LicenceMemberRepository>();
        services.AddSingleton<ILicenceCheckoutRepository, LicenceCheckoutRepository>();
        services.AddSingleton<LicenceCheckoutSweeper>();
        services.AddHostedService(sp => sp.GetRequiredService<LicenceCheckoutSweeper>());
        services.AddSingleton<IAuditEventRepository, AuditEventRepository>();
        services.AddSingleton<IProductRepository, ProductRepository>();
        services.AddSingleton<IProductImageStorage, FileSystemProductImageStorage>();
        services.AddSingleton<IProductFileRepository, ProductFileRepository>();
        services.AddSingleton<IProductFileStorage, FileSystemProductFileStorage>();
        services.AddSingleton<IProductContentImageStorage, FileSystemProductContentImageStorage>();
        services.AddSingleton<IProductContentImageRepository, ProductContentImageRepository>();
        services.AddSingleton<IOrderRepository, OrderRepository>();
        services.AddSingleton<IOrderItemRepository, OrderItemRepository>();
        services.AddSingleton<IInvoiceRepository, InvoiceRepository>();
        services.AddSingleton<ICheckoutAttemptRepository, CheckoutAttemptRepository>();
        services.AddSingleton<IUserRepository, UserRepository>();
        services.AddSingleton<ISessionRefreshTokenRepository, SessionRefreshTokenRepository>();
        services.AddSingleton<ILoginRateLimiter, LoginRateLimiter>();
        services.AddSingleton<ILicenceVerifyRateLimiter, LicenceVerifyRateLimiter>();
        services.AddSingleton<ILicenceCheckoutRateLimiter, LicenceCheckoutRateLimiter>();
        services.AddSingleton<IPaymentGateway, StripePaymentGateway>();
        services.AddSingleton<IOrderFulfillmentService, OrderFulfillmentService>();
    }

    private static byte[] LoadPepper(string pepperPath)
    {
        if (!File.Exists(pepperPath))
        {
            throw new FileNotFoundException($"HMAC pepper not found at '{pepperPath}'. Generate one with the dev tools.", pepperPath);
        }

        var text = File.ReadAllText(pepperPath).Trim();
        try
        {
            return Convert.FromBase64String(text);
        }
        catch (FormatException ex)
        {
            throw new InvalidOperationException($"HMAC pepper at '{pepperPath}' is not valid base64.", ex);
        }
    }
}
