namespace LicenceBackend.Core.Licences;

public sealed record LicenceMember(Guid LicenceId, Guid UserId, Guid AddedBy, DateTimeOffset AddedAt);
