using Dapper;
using LicenceBackend.Core.Common;
using LicenceBackend.Core.Licences;
using Npgsql;

namespace LicenceBackend.Infrastructure.Persistence;

public sealed class LicenceVerificationAttemptRepository(NpgsqlDataSource dataSource)
    : ILicenceVerificationAttemptRepository
{
    public async Task RecordAsync(LicenceVerificationAttempt attempt, CancellationToken cancellationToken)
    {
        const string sql = """
                           INSERT INTO licence_verification_attempts (
                               id, licence_id, product_id_requested, hwid_hmac,
                               source_ip, outcome, denial_reason, attempted_at
                           ) VALUES (
                               @Id, @LicenceId, @ProductIdRequested, @HwidHmac,
                               @SourceIp::inet, @Outcome, @DenialReason, @AttemptedAt
                           );
                           """;

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var command = new CommandDefinition(
            sql,
            new
            {
                attempt.Id,
                attempt.LicenceId,
                attempt.ProductIdRequested,
                attempt.HwidHmac,
                attempt.SourceIp,
                Outcome = OutcomeToString(attempt.Outcome),
                DenialReason = DenialReasonToString(attempt.DenialReason),
                attempt.AttemptedAt
            },
            cancellationToken: cancellationToken);
        await connection.ExecuteAsync(command);
    }

    public async Task<PagedResult<LicenceVerificationAttempt>> ListForLicenceAsync(
        Guid licenceId,
        VerificationAttemptOutcomeFilter filter,
        int limit,
        int offset,
        CancellationToken cancellationToken)
    {
        var outcomeFilter = FilterToSqlText(filter);
        const string sql = """
                           SELECT id, licence_id, product_id_requested, hwid_hmac,
                                  host(source_ip) AS source_ip, outcome, denial_reason, attempted_at
                           FROM licence_verification_attempts
                           WHERE licence_id = @LicenceId
                             AND (@Outcome::text IS NULL OR outcome = @Outcome::text)
                           ORDER BY attempted_at DESC, id DESC
                           LIMIT @Limit OFFSET @Offset;

                           SELECT COUNT(*) FROM licence_verification_attempts
                           WHERE licence_id = @LicenceId
                             AND (@Outcome::text IS NULL OR outcome = @Outcome::text);
                           """;

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var command = new CommandDefinition(
            sql,
            new { LicenceId = licenceId, Outcome = outcomeFilter, Limit = limit, Offset = offset },
            cancellationToken: cancellationToken);
        await using var multi = await connection.QueryMultipleAsync(command);
        var rows = (await multi.ReadAsync<Row>()).ToList();
        var total = await multi.ReadFirstAsync<int>();

        return new PagedResult<LicenceVerificationAttempt>(
            rows.Select(r => r.ToDomain()).ToList(),
            total);
    }

    public async Task<PagedResult<LicenceVerificationAttempt>> ListAsync(
        VerificationAttemptOutcomeFilter filter,
        int limit,
        int offset,
        CancellationToken cancellationToken)
    {
        var outcomeFilter = FilterToSqlText(filter);
        const string sql = """
                           SELECT id, licence_id, product_id_requested, hwid_hmac,
                                  host(source_ip) AS source_ip, outcome, denial_reason, attempted_at
                           FROM licence_verification_attempts
                           WHERE (@Outcome::text IS NULL OR outcome = @Outcome::text)
                           ORDER BY attempted_at DESC, id DESC
                           LIMIT @Limit OFFSET @Offset;

                           SELECT COUNT(*) FROM licence_verification_attempts
                           WHERE (@Outcome::text IS NULL OR outcome = @Outcome::text);
                           """;

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var command = new CommandDefinition(
            sql,
            new { Outcome = outcomeFilter, Limit = limit, Offset = offset },
            cancellationToken: cancellationToken);
        await using var multi = await connection.QueryMultipleAsync(command);
        var rows = (await multi.ReadAsync<Row>()).ToList();
        var total = await multi.ReadFirstAsync<int>();

        return new PagedResult<LicenceVerificationAttempt>(
            rows.Select(r => r.ToDomain()).ToList(),
            total);
    }

    private static string? FilterToSqlText(VerificationAttemptOutcomeFilter filter)
    {
        return filter switch
        {
            VerificationAttemptOutcomeFilter.All => null,
            VerificationAttemptOutcomeFilter.ApprovedOnly => "approved",
            VerificationAttemptOutcomeFilter.DeniedOnly => "denied",
            _ => throw new ArgumentOutOfRangeException(nameof(filter), filter, null)
        };
    }

    public static string OutcomeToString(VerificationOutcome outcome)
    {
        return outcome switch
        {
            VerificationOutcome.Approved => "approved",
            VerificationOutcome.Denied => "denied",
            _ => throw new ArgumentOutOfRangeException(nameof(outcome), outcome, null)
        };
    }

    public static string? DenialReasonToString(VerificationDenialReason? reason)
    {
        return reason switch
        {
            null => null,
            VerificationDenialReason.ProductMismatch => "product_mismatch",
            VerificationDenialReason.LicenceNotUsable => "licence_not_usable",
            VerificationDenialReason.OwnerSuspended => "owner_suspended",
            VerificationDenialReason.IpNotAllowlisted => "ip_not_allowlisted",
            VerificationDenialReason.HwidMissing => "hwid_missing",
            VerificationDenialReason.HwidMismatch => "hwid_mismatch",
            _ => throw new ArgumentOutOfRangeException(nameof(reason), reason, null)
        };
    }

    public static VerificationOutcome ParseOutcome(string value)
    {
        return value switch
        {
            "approved" => VerificationOutcome.Approved,
            "denied" => VerificationOutcome.Denied,
            _ => throw new InvalidOperationException($"Unknown outcome '{value}'.")
        };
    }

    public static VerificationDenialReason? ParseDenialReason(string? value)
    {
        return value switch
        {
            null => null,
            "product_mismatch" => VerificationDenialReason.ProductMismatch,
            "licence_not_usable" => VerificationDenialReason.LicenceNotUsable,
            "owner_suspended" => VerificationDenialReason.OwnerSuspended,
            "ip_not_allowlisted" => VerificationDenialReason.IpNotAllowlisted,
            "hwid_missing" => VerificationDenialReason.HwidMissing,
            "hwid_mismatch" => VerificationDenialReason.HwidMismatch,
            _ => throw new InvalidOperationException($"Unknown denial_reason '{value}'.")
        };
    }

    private sealed record Row(
        Guid Id,
        Guid LicenceId,
        Guid? ProductIdRequested,
        byte[]? HwidHmac,
        string SourceIp,
        string Outcome,
        string? DenialReason,
        DateTime AttemptedAt
    )
    {
        public LicenceVerificationAttempt ToDomain()
        {
            return new LicenceVerificationAttempt(
                Id,
                LicenceId,
                ProductIdRequested,
                HwidHmac,
                SourceIp,
                ParseOutcome(Outcome),
                ParseDenialReason(DenialReason),
                TimestampConversion.ToUtcOffset(AttemptedAt));
        }
    }
}
