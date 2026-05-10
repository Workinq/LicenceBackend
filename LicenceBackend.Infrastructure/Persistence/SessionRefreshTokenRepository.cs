using System.Security.Cryptography;
using Dapper;
using LicenceBackend.Core.Sessions;
using Npgsql;

namespace LicenceBackend.Infrastructure.Persistence;

public sealed class SessionRefreshTokenRepository(NpgsqlDataSource dataSource) : ISessionRefreshTokenRepository
{
    public async Task CreateAsync(SessionRefreshToken token, CancellationToken cancellationToken)
    {
        const string sql = """
                           INSERT INTO session_refresh_tokens (id, user_id, token_hash, issued_at, expires_at, revoked_at, replaced_by)
                           VALUES (@Id, @UserId, @TokenHash, @IssuedAt, @ExpiresAt, @RevokedAt, @ReplacedBy);
                           """;

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var command = new CommandDefinition(
            sql,
            new
            {
                token.Id,
                token.UserId,
                token.TokenHash,
                IssuedAt  = token.IssuedAt.UtcDateTime,
                ExpiresAt = token.ExpiresAt.UtcDateTime,
                RevokedAt = token.RevokedAt?.UtcDateTime,
                token.ReplacedBy
            },
            cancellationToken: cancellationToken);
        await connection.ExecuteAsync(command);
    }

    public async Task<SessionRefreshToken?> FindByHashAsync(byte[] tokenHash, CancellationToken cancellationToken)
    {
        const string sql = """
                           SELECT id, user_id, token_hash, issued_at, expires_at, revoked_at, replaced_by
                           FROM session_refresh_tokens
                           WHERE token_hash = @TokenHash
                           LIMIT 1;
                           """;

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var             command    = new CommandDefinition(sql, new { TokenHash = tokenHash }, cancellationToken: cancellationToken);
        var             row        = await connection.QuerySingleOrDefaultAsync<RefreshRow>(command);
        if (row is null || !CryptographicOperations.FixedTimeEquals(row.TokenHash, tokenHash)) return null;
        return row.ToDomain();
    }

    public async Task<bool> RotateAsync(
        Guid                oldTokenId,
        SessionRefreshToken newToken,
        CancellationToken   cancellationToken)
    {
        // INSERT must run before UPDATE — the UPDATE's replaced_by FK references the new row.
        const string insertSql = """
                                 INSERT INTO session_refresh_tokens (id, user_id, token_hash, issued_at, expires_at, revoked_at, replaced_by)
                                 VALUES (@Id, @UserId, @TokenHash, @IssuedAt, @ExpiresAt, @RevokedAt, @ReplacedBy);
                                 """;

        const string updateSql = """
                                 UPDATE session_refresh_tokens
                                 SET revoked_at = NOW(), replaced_by = @NewId
                                 WHERE id = @OldId AND revoked_at IS NULL;
                                 """;

        await using var connection  = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        try
        {
            await connection.ExecuteAsync(new CommandDefinition(
                                              insertSql,
                                              new
                                              {
                                                  newToken.Id,
                                                  newToken.UserId,
                                                  newToken.TokenHash,
                                                  IssuedAt  = newToken.IssuedAt.UtcDateTime,
                                                  ExpiresAt = newToken.ExpiresAt.UtcDateTime,
                                                  RevokedAt = newToken.RevokedAt?.UtcDateTime,
                                                  newToken.ReplacedBy
                                              },
                                              transaction,
                                              cancellationToken: cancellationToken));

            var rowsAffected = await connection.ExecuteAsync(new CommandDefinition(
                                                                 updateSql,
                                                                 new { NewId = newToken.Id, OldId = oldTokenId },
                                                                 transaction,
                                                                 cancellationToken: cancellationToken));

            if (rowsAffected == 0)
            {
                await transaction.RollbackAsync(cancellationToken);
                return false;
            }

            await transaction.CommitAsync(cancellationToken);
            return true;
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    public async Task RevokeByIdAsync(Guid tokenId, CancellationToken cancellationToken)
    {
        const string sql = """
                           UPDATE session_refresh_tokens
                           SET revoked_at = NOW()
                           WHERE id = @Id AND revoked_at IS NULL;
                           """;

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var             command    = new CommandDefinition(sql, new { Id = tokenId }, cancellationToken: cancellationToken);
        await connection.ExecuteAsync(command);
    }

    public async Task RevokeAllForUserAsync(Guid userId, CancellationToken cancellationToken)
    {
        const string sql = """
                           UPDATE session_refresh_tokens
                           SET revoked_at = NOW()
                           WHERE user_id = @UserId AND revoked_at IS NULL;
                           """;

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var             command    = new CommandDefinition(sql, new { UserId = userId }, cancellationToken: cancellationToken);
        await connection.ExecuteAsync(command);
    }

    private sealed record RefreshRow(
        Guid      Id,
        Guid      UserId,
        byte[]    TokenHash,
        DateTime  IssuedAt,
        DateTime  ExpiresAt,
        DateTime? RevokedAt,
        Guid?     ReplacedBy
    )
    {
        public SessionRefreshToken ToDomain()
        {
            return new SessionRefreshToken(
                Id,
                UserId,
                TokenHash,
                TimestampConversion.ToUtcOffset(IssuedAt),
                TimestampConversion.ToUtcOffset(ExpiresAt),
                TimestampConversion.ToUtcOffset(RevokedAt),
                ReplacedBy);
        }
    }
}
