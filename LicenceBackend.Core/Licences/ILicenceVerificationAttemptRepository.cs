using LicenceBackend.Core.Common;

namespace LicenceBackend.Core.Licences;

public interface ILicenceVerificationAttemptRepository
{
    Task RecordAsync(LicenceVerificationAttempt attempt, CancellationToken cancellationToken);

    Task<PagedResult<LicenceVerificationAttempt>> ListForLicenceAsync(
        Guid                             licenceId,
        VerificationAttemptOutcomeFilter filter,
        int                              limit,
        int                              offset,
        CancellationToken                cancellationToken
    );

    Task<PagedResult<LicenceVerificationAttempt>> ListAsync(
        VerificationAttemptOutcomeFilter filter,
        int                              limit,
        int                              offset,
        CancellationToken                cancellationToken
    );
}
