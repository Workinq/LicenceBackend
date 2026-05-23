using DbUp;
using DbUp.Engine;
using DbUp.Engine.Output;
using Microsoft.Extensions.Logging;

namespace LicenceBackend.Infrastructure.Persistence;

public static class SchemaMigrator
{
    public const string JournalSchema = "public";
    public const string JournalTable = "__schema_versions";

    public static DatabaseUpgradeResult Run(
        string connectionString,
        string migrationsDirectory,
        ILogger? logger = null)
    {
        var upgrader = Build(connectionString, migrationsDirectory, logger);
        return upgrader.PerformUpgrade();
    }

    public static IReadOnlyList<string> GetExecuted(string connectionString, string migrationsDirectory)
    {
        var upgrader = Build(connectionString, migrationsDirectory, logger: null);
        return upgrader.GetExecutedScripts();
    }

    public static IReadOnlyList<string> GetPending(string connectionString, string migrationsDirectory)
    {
        var upgrader = Build(connectionString, migrationsDirectory, logger: null);
        return upgrader.GetScriptsToExecute().Select(s => s.Name).ToList();
    }

    private static UpgradeEngine Build(string connectionString, string migrationsDirectory, ILogger? logger)
    {
        var builder = DeployChanges.To
            .PostgresqlDatabase(connectionString)
            .WithScriptsFromFileSystem(migrationsDirectory)
            .JournalToPostgresqlTable(JournalSchema, JournalTable);

        builder = logger is null
            ? builder.LogToNowhere()
            : builder.LogTo(new MicrosoftLoggerAdapter(logger));

        return builder.Build();
    }

    private sealed class MicrosoftLoggerAdapter(ILogger logger) : IUpgradeLog
    {
        public void LogTrace(string format, params object[] args) =>
            logger.Log(LogLevel.Trace, "{Message}", string.Format(System.Globalization.CultureInfo.InvariantCulture, format, args));
        public void LogDebug(string format, params object[] args) =>
            logger.Log(LogLevel.Debug, "{Message}", string.Format(System.Globalization.CultureInfo.InvariantCulture, format, args));
        public void LogInformation(string format, params object[] args) =>
            logger.Log(LogLevel.Information, "{Message}", string.Format(System.Globalization.CultureInfo.InvariantCulture, format, args));
        public void LogWarning(string format, params object[] args) =>
            logger.Log(LogLevel.Warning, "{Message}", string.Format(System.Globalization.CultureInfo.InvariantCulture, format, args));
        public void LogError(string format, params object[] args) =>
            logger.Log(LogLevel.Error, "{Message}", string.Format(System.Globalization.CultureInfo.InvariantCulture, format, args));
        public void LogError(Exception ex, string format, params object[] args) =>
            logger.Log(LogLevel.Error, ex, "{Message}", string.Format(System.Globalization.CultureInfo.InvariantCulture, format, args));
    }
}
