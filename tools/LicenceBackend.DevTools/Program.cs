using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Dapper;
using LicenceBackend.Infrastructure.Crypto;
using LicenceBackend.Infrastructure.Persistence;
using Npgsql;

const string secretsDir = "./secrets";
const string sessionKeyPrefix = "session-signing-key";
const string licenceVerifyKeyPrefix = "licence-verify-signing-key";
const string pepperPrefix = "licence-key-pepper";
const string defaultMigrationsPath = "./migrations";
const string connEnvVar = "LICENCEBACKEND_POSTGRES";
const string passwordAlphabet = "ABCDEFGHJKMNPQRSTUVWXYZabcdefghjkmnpqrstuvwxyz23456789"; // gitleaks:allow

if (args.Length == 0)
{
    PrintUsage();
    return 0;
}

try
{
    return args[0] switch
    {
        "init-secrets" => await InitSecretsAsync(args),
        "rotate-session-key" => await RotateSigningKeyAsync(args, "SessionSigning", sessionKeyPrefix, "session"),
        "rotate-licence-verify-key" => await RotateSigningKeyAsync(args, "LicenceVerifySigning", licenceVerifyKeyPrefix, "licence-verify"),
        "rotate-pepper" => await RotatePepperAsync(args),
        "migrate" => await MigrateAsync(),
        "migrate-status" => await MigrateStatusAsync(),
        "seed-dev" => await SeedDevAsync(),
        "create-admin" => await CreateAdminAsync(args),
        "list-users" => await ListUsersAsync(args),
        "disable-user" => await DisableUserAsync(args),
        "reset-password" => await ResetPasswordAsync(args),
        "--help" or "-h" or "help" => PrintUsageReturn(),
        _ => UnknownCommand(args[0])
    };
}
catch (Exception ex)
{
    await Console.Error.WriteLineAsync($"error: {ex.Message}");
    return 1;
}

int PrintUsageReturn()
{
    PrintUsage();
    return 0;
}

int UnknownCommand(string cmd)
{
    Console.Error.WriteLine($"unknown command: {cmd}");
    Console.Error.WriteLine();
    PrintUsage();
    return 2;
}

void PrintUsage()
{
    Console.WriteLine("Usage: dotnet run --project tools/LicenceBackend.DevTools -- <command>");
    Console.WriteLine("Commands:");
    Console.WriteLine("  init-secrets   [--force]                         Generate v1 of session signing key, licence-verify signing key, and HMAC pepper into ./secrets/");
    Console.WriteLine("  rotate-session-key       [--kid <name>]          Generate a new session-signing key under a fresh kid (next sequential by default)");
    Console.WriteLine("  rotate-licence-verify-key [--kid <name>]         Generate a new licence-verify signing key under a fresh kid");
    Console.WriteLine("  rotate-pepper            [--version <n>]         Generate a new HMAC pepper at the next sequential version");
    Console.WriteLine("  migrate                                          Apply pending migrations from ./migrations to the configured Postgres (creates DB if missing)");
    Console.WriteLine("  migrate-status                                   List applied + pending migrations without running anything");
    Console.WriteLine("  seed-dev                                         Insert one Product + one active Licence; prints the raw licence key");
    Console.WriteLine("  create-admin   --email <email> [--password <p>] [--force]");
    Console.WriteLine("                                                   Creates (or with --force, upserts) a user with role=admin. If --password is omitted, one is generated.");
    Console.WriteLine("  list-users     [--limit <n>] [--offset <n>]      List users (id, email, role, status). Default limit 50.");
    Console.WriteLine("  disable-user   --email <email>                   Suspend a user and revoke every live refresh token for them.");
    Console.WriteLine("  reset-password --email <email> [--password <p>]  Set a user's password. If --password omitted, a 24-char password is generated and printed once.");
    Console.WriteLine();
    Console.WriteLine("Connection string for migrate/seed-dev/create-admin is resolved in order:");
    Console.WriteLine($"  1. {connEnvVar} environment variable");
    Console.WriteLine("  2. ConnectionStrings:Postgres in dotnet user-secrets for LicenceBackend.Api");
    Console.WriteLine("  3. ConnectionStrings:Postgres in LicenceBackend.Api/appsettings.Development.json");
    Console.WriteLine("  4. ConnectionStrings:Postgres in LicenceBackend.Api/appsettings.json");
}

async Task<int> InitSecretsAsync(string[] cmdArgs)
{
    var force = cmdArgs.Contains("--force");
    Directory.CreateDirectory(secretsDir);

    var sessionV1 = Path.Combine(secretsDir, $"{sessionKeyPrefix}-v1.pem");
    var licenceVerifyV1 = Path.Combine(secretsDir, $"{licenceVerifyKeyPrefix}-v1.pem");
    var pepperV1 = Path.Combine(secretsDir, $"{pepperPrefix}-v1.txt");

    var existing = new[] { sessionV1, licenceVerifyV1, pepperV1 }.Where(File.Exists).ToArray();
    if (existing.Length > 0 && !force)
    {
        await Console.Error.WriteLineAsync("Refusing to overwrite existing v1 secret files. Pass --force to regenerate, or use rotate-* to add new versions:");
        foreach (var f in existing)
        {
            await Console.Error.WriteLineAsync($"  {f}");
        }
        return 1;
    }

    using (var sessionEcdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256))
    {
        await WriteSecretFileAsync(sessionV1, sessionEcdsa.ExportPkcs8PrivateKeyPem() + Environment.NewLine);
    }

    using (var licenceVerifyEcdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256))
    {
        await WriteSecretFileAsync(licenceVerifyV1, licenceVerifyEcdsa.ExportPkcs8PrivateKeyPem() + Environment.NewLine);
    }

    var pepper = RandomNumberGenerator.GetBytes(32);
    await WriteSecretFileAsync(pepperV1, Convert.ToBase64String(pepper) + Environment.NewLine);

    Console.WriteLine($"Wrote session signing key (v1):         {Path.GetFullPath(sessionV1)}");
    Console.WriteLine($"Wrote licence-verify signing key (v1):  {Path.GetFullPath(licenceVerifyV1)}");
    Console.WriteLine($"Wrote HMAC pepper (v1):                 {Path.GetFullPath(pepperV1)}");
    Console.WriteLine();
    Console.WriteLine("All files are gitignored. Back them up if you need this environment reproducible.");
    Console.WriteLine();
    Console.WriteLine("Add to your appsettings.Development.json (or replace existing entries):");
    Console.WriteLine();
    Console.WriteLine("  \"SessionSigning\": {");
    Console.WriteLine($"    \"Keys\": [ {{ \"Kid\": \"session-v1\", \"PrivateKeyPath\": \"../secrets/{sessionKeyPrefix}-v1.pem\" }} ],");
    Console.WriteLine("    \"ActiveKid\": \"session-v1\"");
    Console.WriteLine("  },");
    Console.WriteLine("  \"LicenceVerifySigning\": {");
    Console.WriteLine($"    \"Keys\": [ {{ \"Kid\": \"licence-verify-v1\", \"PrivateKeyPath\": \"../secrets/{licenceVerifyKeyPrefix}-v1.pem\" }} ],");
    Console.WriteLine("    \"ActiveKid\": \"licence-verify-v1\"");
    Console.WriteLine("  },");
    Console.WriteLine("  \"Licence\": {");
    Console.WriteLine($"    \"Peppers\": [ {{ \"Version\": 1, \"Path\": \"../secrets/{pepperPrefix}-v1.txt\" }} ],");
    Console.WriteLine("    \"ActivePepperVersion\": 1");
    Console.WriteLine("  }");
    return 0;
}

async Task<int> RotateSigningKeyAsync(string[] cmdArgs, string sectionName, string filePrefix, string kidPrefix)
{
    Directory.CreateDirectory(secretsDir);
    string? overrideKid = null;
    for (var i = 1; i < cmdArgs.Length; i++)
        if (cmdArgs[i] == "--kid" && i + 1 < cmdArgs.Length)
            overrideKid = cmdArgs[++i];

    var nextVersion = NextVersion(filePrefix, ".pem");
    var newKid = overrideKid ?? $"{kidPrefix}-v{nextVersion}";
    var newPath = Path.Combine(secretsDir, $"{filePrefix}-{newKid}.pem");

    if (File.Exists(newPath))
    {
        await Console.Error.WriteLineAsync($"Refusing to overwrite existing key at {newPath}.");
        return 1;
    }

    using (var ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256))
    {
        await WriteSecretFileAsync(newPath, ecdsa.ExportPkcs8PrivateKeyPem() + Environment.NewLine);
    }

    Console.WriteLine($"Wrote {sectionName} key:  {Path.GetFullPath(newPath)}");
    Console.WriteLine();
    Console.WriteLine($"Add this entry to {sectionName}:Keys in appsettings.json:");
    Console.WriteLine($"  {{ \"Kid\": \"{newKid}\", \"PrivateKeyPath\": \"../secrets/{Path.GetFileName(newPath)}\" }}");
    Console.WriteLine();
    Console.WriteLine("When you are ready to start signing under the new key, set:");
    Console.WriteLine($"  {sectionName}:ActiveKid = \"{newKid}\"");
    Console.WriteLine();
    Console.WriteLine("Keep the previous key entry in the Keys list until tokens signed under it have all expired.");
    return 0;
}

async Task<int> RotatePepperAsync(string[] cmdArgs)
{
    Directory.CreateDirectory(secretsDir);
    short? overrideVersion = null;
    for (var i = 1; i < cmdArgs.Length; i++)
        if (cmdArgs[i] == "--version" && i + 1 < cmdArgs.Length)
        {
            if (short.TryParse(cmdArgs[++i], out var parsed) && parsed > 0)
            {
                overrideVersion = parsed;
            }
            else
            {
                await Console.Error.WriteLineAsync("--version must be a positive integer.");
                return 2;
            }
        }

    var nextVersion = overrideVersion ?? checked((short)NextVersion(pepperPrefix, ".txt"));
    var newPath = Path.Combine(secretsDir, $"{pepperPrefix}-v{nextVersion}.txt");

    if (File.Exists(newPath))
    {
        await Console.Error.WriteLineAsync($"Refusing to overwrite existing pepper at {newPath}.");
        return 1;
    }

    var pepper = RandomNumberGenerator.GetBytes(32);
    await WriteSecretFileAsync(newPath, Convert.ToBase64String(pepper) + Environment.NewLine);

    Console.WriteLine($"Wrote HMAC pepper (v{nextVersion}):  {Path.GetFullPath(newPath)}");
    Console.WriteLine();
    Console.WriteLine("Add this entry to Licence:Peppers in appsettings.json:");
    Console.WriteLine($"  {{ \"Version\": {nextVersion}, \"Path\": \"../secrets/{Path.GetFileName(newPath)}\" }}");
    Console.WriteLine();
    Console.WriteLine("When you are ready to start hashing new licences/HWIDs under the new pepper, set:");
    Console.WriteLine($"  Licence:ActivePepperVersion = {nextVersion}");
    Console.WriteLine();
    Console.WriteLine("Keep the previous pepper entry in the Peppers list until every licence/HWID hashed under it has been revoked or re-issued.");
    return 0;
}

int NextVersion(string filePrefix, string extension)
{
    if (!Directory.Exists(secretsDir)) return 1;

    var pattern = new Regex($"^{Regex.Escape(filePrefix)}-(?:[a-z0-9-]*?-)?v(\\d+){Regex.Escape(extension)}$",
                            RegexOptions.IgnoreCase);
    var max = 0;
    foreach (var file in Directory.EnumerateFiles(secretsDir))
    {
        var match = pattern.Match(Path.GetFileName(file));
        if (match.Success && int.TryParse(match.Groups[1].Value, out var n) && n > max) max = n;
    }

    return max + 1;
}

async Task<int> MigrateAsync()
{
    var connectionString = RequireConnectionString();
    if (!Directory.Exists(defaultMigrationsPath))
    {
        await Console.Error.WriteLineAsync($"Migrations directory not found at {defaultMigrationsPath}. Run from the repository root.");
        return 1;
    }

    await EnsureDatabaseExistsAsync(connectionString);

    var result = SchemaMigrator.Run(connectionString, defaultMigrationsPath);
    if (!result.Successful)
    {
        await Console.Error.WriteLineAsync($"Migration failed in script: {result.ErrorScript?.Name}");
        await Console.Error.WriteLineAsync(result.Error.ToString());
        return 1;
    }

    var applied = result.Scripts.ToList();
    if (applied.Count == 0)
    {
        Console.WriteLine("No migrations to apply.");
        return 0;
    }

    Console.WriteLine($"Applied {applied.Count} migration(s):");
    foreach (var s in applied) Console.WriteLine($"  {s.Name}");
    return 0;
}

Task<int> MigrateStatusAsync()
{
    var connectionString = RequireConnectionString();
    if (!Directory.Exists(defaultMigrationsPath))
    {
        Console.Error.WriteLine($"Migrations directory not found at {defaultMigrationsPath}. Run from the repository root.");
        return Task.FromResult(1);
    }

    var executed = SchemaMigrator.GetExecuted(connectionString, defaultMigrationsPath);
    var pending = SchemaMigrator.GetPending(connectionString, defaultMigrationsPath);

    Console.WriteLine($"Applied ({executed.Count}):");
    if (executed.Count == 0) Console.WriteLine("  (none)");
    foreach (var name in executed) Console.WriteLine($"  {name}");

    Console.WriteLine();
    Console.WriteLine($"Pending ({pending.Count}):");
    if (pending.Count == 0) Console.WriteLine("  (none)");
    foreach (var name in pending) Console.WriteLine($"  {name}");
    return Task.FromResult(0);
}

async Task EnsureDatabaseExistsAsync(string connectionString)
{
    var builder = new NpgsqlConnectionStringBuilder(connectionString);
    var targetDb = builder.Database;
    if (string.IsNullOrWhiteSpace(targetDb)) return;

    builder.Database = "postgres";
    await using var admin = new NpgsqlConnection(builder.ConnectionString);
    await admin.OpenAsync();
    var exists = await admin.ExecuteScalarAsync<bool>(
                     "SELECT EXISTS (SELECT 1 FROM pg_database WHERE datname = @Db);",
                     new { Db = targetDb });
    if (exists) return;

    // CREATE DATABASE doesn't allow parameter substitution and the identifier must be quoted.
    var quoted = "\"" + targetDb.Replace("\"", "\"\"") + "\"";
    await admin.ExecuteAsync($"CREATE DATABASE {quoted};");
    Console.WriteLine($"Created database '{targetDb}'.");
}

async Task<int> SeedDevAsync()
{
    const string userEmail = "seed-user@test.local";
    const string productSlug = "testproduct";
    var emailLower = userEmail.ToLowerInvariant();

    var connectionString = RequireConnectionString();
    var pepperSet = LoadPepperSetForDev();
    var licenceHasher = new HmacLicenceKeyHasher(pepperSet);

    await using var connection = new NpgsqlConnection(connectionString);
    await connection.OpenAsync();

    var existingUserId = await connection.QuerySingleOrDefaultAsync<Guid?>(
                             "SELECT id FROM users WHERE email_lower = @EmailLower LIMIT 1;",
                             new { EmailLower = emailLower });
    var existingProductId = await connection.QuerySingleOrDefaultAsync<Guid?>(
                                "SELECT id FROM products WHERE slug = @Slug LIMIT 1;",
                                new { Slug = productSlug });
    var existingLicenceId = existingUserId is { } uid && existingProductId is { } pid
                                ? await connection.QuerySingleOrDefaultAsync<Guid?>(
                                      "SELECT id FROM licences WHERE user_id = @UserId AND product_id = @ProductId LIMIT 1;",
                                      new { UserId = uid, ProductId = pid })
                                : null;

    if (existingUserId.HasValue && existingProductId.HasValue && existingLicenceId.HasValue)
    {
        Console.WriteLine("Dev seed already present (idempotent run, no changes):");
        Console.WriteLine($"  user:     id={existingUserId.Value}  email={userEmail}");
        Console.WriteLine($"  product:  id={existingProductId.Value}  slug={productSlug}");
        Console.WriteLine($"  licence:  id={existingLicenceId.Value}");
        Console.WriteLine();
        Console.WriteLine("Plaintext password and licence key are not recoverable. Re-run after `apply-schema` to regenerate.");
        return 0;
    }

    var passwordHasher = new Argon2IdPasswordHasher();
    var keyGenerator = new LicenceKeyGenerator();
    var userPassword = GenerateReadablePassword(24);
    var userPasswordHash = passwordHasher.Hash(userPassword);

    var userId = existingUserId ?? Guid.NewGuid();
    var productId = existingProductId ?? Guid.NewGuid();
    var licenceId = existingLicenceId ?? Guid.NewGuid();
    var licenceKey = keyGenerator.Generate();
    var pepperedHmac = licenceHasher.HashWithActive(licenceKey);

    if (!existingUserId.HasValue)
        await connection.ExecuteAsync(
            """
            INSERT INTO users (id, email, email_lower, password_hash, display_name, role, status)
            VALUES (@Id, @Email, @EmailLower, @Hash, 'Seed User', 'user', 'active');
            """,
            new { Id = userId, Email = userEmail, EmailLower = emailLower, Hash = userPasswordHash });

    if (!existingProductId.HasValue)
        await connection.ExecuteAsync(
            "INSERT INTO products (id, slug, display_name) VALUES (@Id, @Slug, @DisplayName)",
            new { Id = productId, Slug = productSlug, DisplayName = "Test Product" });

    if (!existingLicenceId.HasValue)
        await connection.ExecuteAsync(
            """
            INSERT INTO licences (id, product_id, user_id, key_hmac, key_hmac_pepper_version, status)
            VALUES (@Id, @ProductId, @UserId, @KeyHmac, @KeyHmacPepperVersion, 'active');
            """,
            new
            {
                Id = licenceId,
                ProductId = productId,
                UserId = userId,
                KeyHmac = pepperedHmac.Hmac,
                KeyHmacPepperVersion = pepperedHmac.PepperVersion
            });

    Console.WriteLine($"Seeded user:     id={userId}  email={userEmail}  role=user  status=active");
    Console.WriteLine($"Seeded product:  id={productId}  slug={productSlug}");
    Console.WriteLine($"Seeded licence:  id={licenceId}  status=active  owner={userEmail}  pepperVersion={pepperedHmac.PepperVersion}");
    if (!existingUserId.HasValue)
    {
        Console.WriteLine();
        Console.WriteLine("Copy the user password (shown once; only its Argon2id hash is stored):");
        Console.WriteLine($"  {userPassword}");
    }

    if (!existingLicenceId.HasValue)
    {
        Console.WriteLine();
        Console.WriteLine("Copy the licence key (shown once; only its HMAC is stored):");
        Console.WriteLine($"  {licenceKey}");
    }

    return 0;
}

async Task<int> CreateAdminAsync(string[] cmdArgs)
{
    string? email = null;
    string? password = null;
    var force = false;

    for (var i = 1; i < cmdArgs.Length; i++)
        switch (cmdArgs[i])
        {
            case "--email":
                if (i + 1 >= cmdArgs.Length)
                {
                    await Console.Error.WriteLineAsync("--email requires a value.");
                    return 2;
                }

                email = cmdArgs[++i];
                break;
            case "--password":
                if (i + 1 >= cmdArgs.Length)
                {
                    await Console.Error.WriteLineAsync("--password requires a value.");
                    return 2;
                }

                password = cmdArgs[++i];
                break;
            case "--force":
                force = true;
                break;
            default:
                await Console.Error.WriteLineAsync($"unknown flag: {cmdArgs[i]}");
                return 2;
        }

    if (string.IsNullOrWhiteSpace(email))
    {
        await Console.Error.WriteLineAsync("--email is required.");
        return 2;
    }

    var passwordGenerated = false;
    if (string.IsNullOrEmpty(password))
    {
        password = GenerateReadablePassword(24);
        passwordGenerated = true;
    }
    else if (password.Length < 12)
    {
        await Console.Error.WriteLineAsync("Password must be at least 12 characters.");
        return 2;
    }

    var connectionString = RequireConnectionString();
    var hasher = new Argon2IdPasswordHasher();
    var hash = hasher.Hash(password);
    var emailTrimmed = email.Trim();
    var emailLower = emailTrimmed.ToLowerInvariant();

    await using var connection = new NpgsqlConnection(connectionString);
    await connection.OpenAsync();

    var existingId = await connection.QuerySingleOrDefaultAsync<Guid?>(
                         "SELECT id FROM users WHERE email_lower = @EmailLower LIMIT 1;",
                         new { EmailLower = emailLower });

    if (existingId.HasValue && !force)
    {
        await Console.Error.WriteLineAsync($"A user with email '{emailTrimmed}' already exists. Use --force to update password and ensure admin role.");
        return 1;
    }

    if (existingId.HasValue)
    {
        await connection.ExecuteAsync(
            """
            UPDATE users
            SET password_hash = @Hash,
                role = 'admin',
                updated_at = NOW()
            WHERE id = @Id;
            """,
            new { Id = existingId.Value, Hash = hash });

        Console.WriteLine($"Updated existing user:  id={existingId.Value}  email={emailTrimmed}  role=admin");
    }
    else
    {
        var id = Guid.NewGuid();
        await connection.ExecuteAsync(
            """
            INSERT INTO users (id, email, email_lower, password_hash, display_name, role, created_at, updated_at)
            VALUES (@Id, @Email, @EmailLower, @Hash, NULL, 'admin', NOW(), NOW());
            """,
            new { Id = id, Email = emailTrimmed, EmailLower = emailLower, Hash = hash });

        Console.WriteLine($"Created admin user:  id={id}  email={emailTrimmed}  role=admin");
    }

    if (passwordGenerated)
    {
        Console.WriteLine();
        Console.WriteLine("Generated password (shown once; only its Argon2id hash is stored):");
        Console.WriteLine($"  {password}");
    }

    return 0;
}

async Task<int> ListUsersAsync(string[] cmdArgs)
{
    var limit = 50;
    var offset = 0;
    for (var i = 1; i < cmdArgs.Length; i++)
        switch (cmdArgs[i])
        {
            case "--limit":
                if (i + 1 >= cmdArgs.Length || !int.TryParse(cmdArgs[++i], out limit) || limit <= 0)
                {
                    await Console.Error.WriteLineAsync("--limit must be a positive integer.");
                    return 2;
                }

                break;
            case "--offset":
                if (i + 1 >= cmdArgs.Length || !int.TryParse(cmdArgs[++i], out offset) || offset < 0)
                {
                    await Console.Error.WriteLineAsync("--offset must be a non-negative integer.");
                    return 2;
                }

                break;
            default:
                await Console.Error.WriteLineAsync($"unknown flag: {cmdArgs[i]}");
                return 2;
        }

    var connectionString = RequireConnectionString();
    await using var connection = new NpgsqlConnection(connectionString);
    await connection.OpenAsync();

    var rows = (await connection.QueryAsync<(Guid id, string email, string role, string status, DateTime created_at)>(
                    """
                    SELECT id, email, role, status, created_at
                    FROM users
                    ORDER BY created_at DESC
                    LIMIT @Limit OFFSET @Offset;
                    """,
                    new { Limit = limit, Offset = offset })).ToList();

    if (rows.Count == 0)
    {
        Console.WriteLine("(no users)");
        return 0;
    }

    Console.WriteLine($"{"id",-36}  {"role",-7}  {"status",-9}  {"created",-20}  email");
    foreach (var row in rows) Console.WriteLine($"{row.id}  {row.role,-7}  {row.status,-9}  {row.created_at:yyyy-MM-dd HH:mm:ss}  {row.email}");
    return 0;
}

async Task<int> DisableUserAsync(string[] cmdArgs)
{
    string? email = null;
    for (var i = 1; i < cmdArgs.Length; i++)
        if (cmdArgs[i] == "--email" && i + 1 < cmdArgs.Length)
        {
            email = cmdArgs[++i];
        }
        else
        {
            await Console.Error.WriteLineAsync($"unknown flag: {cmdArgs[i]}");
            return 2;
        }

    if (string.IsNullOrWhiteSpace(email))
    {
        await Console.Error.WriteLineAsync("--email is required.");
        return 2;
    }

    var connectionString = RequireConnectionString();
    var emailLower = email.Trim().ToLowerInvariant();
    await using var connection = new NpgsqlConnection(connectionString);
    await connection.OpenAsync();

    var user = await connection.QuerySingleOrDefaultAsync<(Guid id, string status)?>(
                   "SELECT id, status FROM users WHERE email_lower = @EmailLower LIMIT 1;",
                   new { EmailLower = emailLower });

    if (user is null)
    {
        await Console.Error.WriteLineAsync($"No user with email '{email}'.");
        return 1;
    }

    if (user.Value.status == "suspended")
    {
        Console.WriteLine($"User {user.Value.id} is already suspended.");
        return 0;
    }

    await using var transaction = await connection.BeginTransactionAsync();
    try
    {
        await connection.ExecuteAsync(
            "UPDATE users SET status = 'suspended', updated_at = NOW() WHERE id = @Id;",
            new { Id = user.Value.id }, transaction);
        await connection.ExecuteAsync(
            """
            INSERT INTO user_status_history (id, user_id, previous_status, new_status, changed_by, reason)
            VALUES (@Id, @UserId, @Prev, 'suspended', @UserId, 'devtools-disable-user');
            """,
            new { Id = Guid.NewGuid(), UserId = user.Value.id, Prev = user.Value.status }, transaction);
        await connection.ExecuteAsync(
            "UPDATE session_refresh_tokens SET revoked_at = NOW() WHERE user_id = @UserId AND revoked_at IS NULL;",
            new { UserId = user.Value.id }, transaction);
        await transaction.CommitAsync();
    }
    catch
    {
        await transaction.RollbackAsync();
        throw;
    }

    Console.WriteLine($"Suspended user {user.Value.id} ({email}). Every live refresh token for this user has been revoked.");
    return 0;
}

async Task<int> ResetPasswordAsync(string[] cmdArgs)
{
    string? email = null;
    string? password = null;
    for (var i = 1; i < cmdArgs.Length; i++)
        switch (cmdArgs[i])
        {
            case "--email":
                if (i + 1 >= cmdArgs.Length)
                {
                    await Console.Error.WriteLineAsync("--email requires a value.");
                    return 2;
                }

                email = cmdArgs[++i];
                break;
            case "--password":
                if (i + 1 >= cmdArgs.Length)
                {
                    await Console.Error.WriteLineAsync("--password requires a value.");
                    return 2;
                }

                password = cmdArgs[++i];
                break;
            default:
                await Console.Error.WriteLineAsync($"unknown flag: {cmdArgs[i]}");
                return 2;
        }

    if (string.IsNullOrWhiteSpace(email))
    {
        await Console.Error.WriteLineAsync("--email is required.");
        return 2;
    }

    var passwordGenerated = false;
    if (string.IsNullOrEmpty(password))
    {
        password = GenerateReadablePassword(24);
        passwordGenerated = true;
    }
    else if (password.Length < 12)
    {
        await Console.Error.WriteLineAsync("Password must be at least 12 characters.");
        return 2;
    }

    var connectionString = RequireConnectionString();
    var emailLower = email.Trim().ToLowerInvariant();
    var hasher = new Argon2IdPasswordHasher();
    var hash = hasher.Hash(password);

    await using var connection = new NpgsqlConnection(connectionString);
    await connection.OpenAsync();

    var userId = await connection.QuerySingleOrDefaultAsync<Guid?>(
                     "SELECT id FROM users WHERE email_lower = @EmailLower LIMIT 1;",
                     new { EmailLower = emailLower });
    if (!userId.HasValue)
    {
        await Console.Error.WriteLineAsync($"No user with email '{email}'.");
        return 1;
    }

    await using var transaction = await connection.BeginTransactionAsync();
    try
    {
        await connection.ExecuteAsync(
            "UPDATE users SET password_hash = @Hash, updated_at = NOW() WHERE id = @Id;",
            new { Id = userId.Value, Hash = hash }, transaction);
        await connection.ExecuteAsync(
            "UPDATE session_refresh_tokens SET revoked_at = NOW() WHERE user_id = @UserId AND revoked_at IS NULL;",
            new { UserId = userId.Value }, transaction);
        await transaction.CommitAsync();
    }
    catch
    {
        await transaction.RollbackAsync();
        throw;
    }

    Console.WriteLine($"Password reset for user {userId.Value} ({email}). Every live refresh token has been revoked.");
    if (passwordGenerated)
    {
        Console.WriteLine();
        Console.WriteLine("Generated password (shown once; only its Argon2id hash is stored):");
        Console.WriteLine($"  {password}");
    }

    return 0;
}

string RequireConnectionString()
{
    var fromEnv = Environment.GetEnvironmentVariable(connEnvVar);
    if (!string.IsNullOrWhiteSpace(fromEnv)) return fromEnv;

    var fromUserSecrets = TryReadConnectionStringFromUserSecrets();
    if (!string.IsNullOrWhiteSpace(fromUserSecrets)) return fromUserSecrets;

    var fromSettings = TryReadConnectionStringFromApiAppSettings();
    if (!string.IsNullOrWhiteSpace(fromSettings)) return fromSettings;

    throw new InvalidOperationException(
        $"No connection string found. Set {connEnvVar}, run 'dotnet user-secrets set ConnectionStrings:Postgres ... -p LicenceBackend.Api', or set ConnectionStrings:Postgres in LicenceBackend.Api/appsettings.Development.json.");
}

string? TryReadConnectionStringFromUserSecrets()
{
    const string apiCsproj = "./LicenceBackend.Api/LicenceBackend.Api.csproj";
    if (!File.Exists(apiCsproj)) return null;

    string userSecretsId;
    try
    {
        var match = new Regex(@"<UserSecretsId>\s*([^<\s]+)\s*</UserSecretsId>", RegexOptions.IgnoreCase)
            .Match(File.ReadAllText(apiCsproj));
        if (!match.Success) return null;
        userSecretsId = match.Groups[1].Value.Trim();
    }
    catch (IOException)
    {
        return null;
    }

    string secretsPath;
    if (OperatingSystem.IsWindows())
    {
        var appData = Environment.GetEnvironmentVariable("APPDATA");
        if (string.IsNullOrEmpty(appData)) return null;
        secretsPath = Path.Combine(appData, "Microsoft", "UserSecrets", userSecretsId, "secrets.json");
    }
    else
    {
        var home = Environment.GetEnvironmentVariable("HOME");
        if (string.IsNullOrEmpty(home)) return null;
        secretsPath = Path.Combine(home, ".microsoft", "usersecrets", userSecretsId, "secrets.json");
    }

    if (!File.Exists(secretsPath)) return null;

    try
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(secretsPath));

        if (doc.RootElement.TryGetProperty("ConnectionStrings:Postgres", out var flat)
            && flat.ValueKind == JsonValueKind.String)
        {
            var v = flat.GetString();
            if (!string.IsNullOrWhiteSpace(v)) return v;
        }

        if (doc.RootElement.TryGetProperty("ConnectionStrings", out var nested)
            && nested.TryGetProperty("Postgres", out var postgres)
            && postgres.ValueKind == JsonValueKind.String)
        {
            var v = postgres.GetString();
            if (!string.IsNullOrWhiteSpace(v)) return v;
        }
    }
    catch (JsonException)
    {
        return null;
    }

    return null;
}

string? TryReadConnectionStringFromApiAppSettings()
{
    var candidates = new[]
    {
        "./LicenceBackend.Api/appsettings.Development.json",
        "./LicenceBackend.Api/appsettings.json"
    };

    foreach (var path in candidates)
    {
        if (!File.Exists(path)) continue;
        try
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(path));
            if (doc.RootElement.TryGetProperty("ConnectionStrings", out var connStrings)
                && connStrings.TryGetProperty("Postgres", out var postgres)
                && postgres.ValueKind == JsonValueKind.String)
            {
                var value = postgres.GetString();
                if (!string.IsNullOrWhiteSpace(value)) return value;
            }
        }
        catch (JsonException)
        {
            // ignore malformed file, caller falls through to next candidate
        }
    }

    return null;
}

HmacPepperSet LoadPepperSetForDev()
{
    var pepperFiles = Directory.Exists(secretsDir)
                          ? Directory.EnumerateFiles(secretsDir, $"{pepperPrefix}-v*.txt").ToArray()
                          : Array.Empty<string>();
    if (pepperFiles.Length == 0)
        throw new FileNotFoundException(
            $"No pepper files found under {secretsDir} matching {pepperPrefix}-v*.txt. Run 'init-secrets' first.",
            Path.Combine(secretsDir, $"{pepperPrefix}-v1.txt"));

    var versionPattern = new Regex($"^{Regex.Escape(pepperPrefix)}-v(\\d+)\\.txt$", RegexOptions.IgnoreCase);
    var peppers = new Dictionary<short, byte[]>();
    short maxVersion = 0;
    foreach (var file in pepperFiles)
    {
        var match = versionPattern.Match(Path.GetFileName(file));
        if (!match.Success || !short.TryParse(match.Groups[1].Value, out var version)) continue;

        var text = File.ReadAllText(file).Trim();
        peppers[version] = Convert.FromBase64String(text);
        if (version > maxVersion) maxVersion = version;
    }

    return new HmacPepperSet(peppers, maxVersion);
}

string GenerateReadablePassword(int length)
{
    Span<byte> bytes = stackalloc byte[length];
    RandomNumberGenerator.Fill(bytes);
    var sb = new StringBuilder(length);
    for (var i = 0; i < length; i++) sb.Append(passwordAlphabet[bytes[i] % passwordAlphabet.Length]);
    return sb.ToString();
}

async Task WriteSecretFileAsync(string path, string content)
{
    await File.WriteAllTextAsync(path, content);
    if (!OperatingSystem.IsWindows()) File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite);
}
