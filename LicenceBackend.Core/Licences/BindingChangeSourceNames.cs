namespace LicenceBackend.Core.Licences;

public static class BindingChangeSourceNames
{
    public const string Admin = "admin";
    public const string FirstUse = "first_use";

    public static string ToString(BindingChangeSource source)
    {
        return source switch
        {
            BindingChangeSource.Admin => Admin,
            BindingChangeSource.FirstUse => FirstUse,
            _ => throw new ArgumentOutOfRangeException(nameof(source), source, null)
        };
    }
}
