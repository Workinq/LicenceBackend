namespace LicenceBackend.Infrastructure.Options;

public sealed class LicenceOptions
{
    public const string SectionName = "Licence";

    public IList<PepperEntry> Peppers { get; init; } = new List<PepperEntry>();
    public short ActivePepperVersion { get; init; }
}
