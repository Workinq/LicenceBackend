using System.Net;
using LicenceBackend.Api;
using LicenceBackend.Api.OpenApi;
using LicenceBackend.Infrastructure;
using LicenceBackend.Infrastructure.Options;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Mvc;
using Scalar.AspNetCore;
using Serilog;
using IPNetwork = Microsoft.AspNetCore.HttpOverrides.IPNetwork;

Log.Logger = new LoggerConfiguration()
             .WriteTo
             .Console()
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
               document.Info.Title = "LicenceBackend API";
               document.Info.Version = "1.0.0";
               return Task.CompletedTask;
           }
        );
        options.AddSchemaTransformer<StringLengthSchemaTransformer>();
    });
    builder.Services.AddProblemDetails();
    builder.Services.AddLicenceBackendInfrastructure(builder.Configuration);

    builder.Services.AddLicenceBackendJwtBearer();
    builder.Services.AddAuthorization();

    builder.Services.Configure<ForwardedHeadersOptions>(options =>
    {
        options.ForwardedHeaders = ForwardedHeaders.XForwardedFor;
        options.KnownNetworks.Clear();
        options.KnownProxies.Clear();
        options.KnownNetworks.Add(new IPNetwork(IPAddress.Loopback, 8));
        options.KnownNetworks.Add(new IPNetwork(IPAddress.IPv6Loopback, 128));
    });

    var rateLimitingOptions = builder.Configuration
                                     .GetSection(RateLimitingOptions.SectionName)
                                     .Get<RateLimitingOptions>() ?? new RateLimitingOptions();

    if (rateLimitingOptions.Enabled)
        builder.Services.AddLicenceBackendRateLimiter(rateLimitingOptions);

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

    await app.RunAsync();
}
catch (Exception ex) when (ex is not HostAbortedException)
{
    Log.Fatal(ex, "LicenceBackend.Api terminated unexpectedly");
    throw new HostStartupException("LicenceBackend.Api terminated unexpectedly", ex);
}
finally
{
    await Log.CloseAndFlushAsync();
}

public abstract partial class Program;

internal sealed class HostStartupException : Exception
{
    public HostStartupException(string message, Exception inner) : base(message, inner) { }
}
