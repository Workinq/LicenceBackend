namespace LicenceBackend.Core.Licences;

public sealed record LicenceBindingHistoryEntry(
    Guid                Id,
    Guid                LicenceId,
    LicenceBindingType  BindingType,
    string?             PreviousValueJson,
    string?             NewValueJson,
    BindingChangeSource ChangeSource,
    Guid?               ChangedByUserId,
    DateTimeOffset      ChangedAt,
    string?             Reason
);
