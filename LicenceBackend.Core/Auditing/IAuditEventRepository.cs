using System.Data;
using LicenceBackend.Core.Common;

namespace LicenceBackend.Core.Auditing;

public interface IAuditEventRepository
{
    Task RecordAsync(AuditEvent evt, CancellationToken cancellationToken);

    Task RecordInTxAsync(IDbConnection connection, IDbTransaction transaction, AuditEvent evt, CancellationToken cancellationToken);

    Task<PagedResult<AuditEvent>> QueryAsync(
        string? subjectType,
        Guid? subjectId,
        IReadOnlyList<string>? eventTypes,
        int limit,
        int offset,
        CancellationToken cancellationToken
    );

    Task<PagedResult<AuditEvent>> QueryVerifiesAsync(
        Guid? licenceId,
        string? outcome,
        int limit,
        int offset,
        CancellationToken cancellationToken
    );
}
