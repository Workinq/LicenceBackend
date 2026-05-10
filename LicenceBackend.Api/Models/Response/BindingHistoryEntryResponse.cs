using System.Text.Json;

namespace LicenceBackend.Api.Models.Response;

public sealed record BindingHistoryEntryResponse(
    Guid Id,
    string BindingType,
    JsonElement? PreviousValue,
    JsonElement? NewValue,
    string ChangeSource,
    Guid? ChangedByUserId,
    DateTimeOffset ChangedAt,
    string? Reason
);
