namespace LicenceBackend.Core.Licences;

public static class BindingTypeNames
{
    public const string Hwid = "hwid";
    public const string IpAllowlist = "ip_allowlist";

    public static string ToString(LicenceBindingType type)
    {
        return type switch
        {
            LicenceBindingType.Hwid => Hwid,
            LicenceBindingType.IpAllowlist => IpAllowlist,
            _ => throw new ArgumentOutOfRangeException(nameof(type), type, null)
        };
    }
}
