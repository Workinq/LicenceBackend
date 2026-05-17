namespace LicenceBackend.Core.Licences;

public sealed record UserLicence(Licence Licence, string Relationship);

public static class UserLicenceRelationships
{
    public const string Owner = "owner";
    public const string Member = "member";
}
