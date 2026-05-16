using System.Text.Json;

namespace LicenceBackend.Core.Auditing.Payloads;

public sealed record LicenceBindingChangedPayload(
    string BindingType,
    string ChangeSource,
    JsonElement? PreviousValue,
    JsonElement? NewValue
);
