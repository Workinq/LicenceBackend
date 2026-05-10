namespace LicenceBackend.Infrastructure.Persistence;

internal static class TimestampConversion
{
    public static DateTimeOffset ToUtcOffset(DateTime value)
    {
        return new DateTimeOffset(DateTime.SpecifyKind(value, DateTimeKind.Utc));
    }

    public static DateTimeOffset? ToUtcOffset(DateTime? value)
    {
        return value.HasValue ? ToUtcOffset(value.Value) : null;
    }
}
