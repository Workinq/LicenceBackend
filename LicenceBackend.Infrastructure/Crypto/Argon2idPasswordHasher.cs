using System.Security.Cryptography;
using System.Text;
using Konscious.Security.Cryptography;
using LicenceBackend.Core.Users;

namespace LicenceBackend.Infrastructure.Crypto;

public sealed class Argon2IdPasswordHasher : IPasswordHasher
{
    private const int DefaultIterations  = 3;
    private const int DefaultMemoryKiB   = 65_536;
    private const int DefaultParallelism = 1;
    private const int SaltBytes          = 16;
    private const int HashBytes          = 32;
    private const int ArgonVersion       = 19;

    private static readonly string DummyHash = ComputeDummyHash();

    public string Hash(string password)
    {
        ArgumentException.ThrowIfNullOrEmpty(password);

        var salt = RandomNumberGenerator.GetBytes(SaltBytes);
        var hash = ComputeHash(password, salt, DefaultIterations, DefaultMemoryKiB, DefaultParallelism, HashBytes);
        return Encode(DefaultIterations, DefaultMemoryKiB, DefaultParallelism, salt, hash);
    }

    public bool Verify(string password, string encodedHash)
    {
        if (string.IsNullOrEmpty(password) || string.IsNullOrEmpty(encodedHash)) return false;

        if (!TryDecode(encodedHash, out var iterations, out var memoryKiB, out var parallelism, out var salt, out var expected)) return false;

        var actual = ComputeHash(password, salt, iterations, memoryKiB, parallelism, expected.Length);
        return CryptographicOperations.FixedTimeEquals(actual, expected);
    }

    public void VerifyDummy(string password)
    {
        _ = Verify(password, DummyHash);
    }

    private static string ComputeDummyHash()
    {
        var salt = RandomNumberGenerator.GetBytes(SaltBytes);
        var hash = ComputeHash("verify-timing-dummy", salt, DefaultIterations, DefaultMemoryKiB, DefaultParallelism, HashBytes);
        return Encode(DefaultIterations, DefaultMemoryKiB, DefaultParallelism, salt, hash);
    }

    private static byte[] ComputeHash(string password, byte[] salt, int iterations, int memoryKiB, int parallelism, int hashLength)
    {
        using var argon2 = new Argon2id(Encoding.UTF8.GetBytes(password))
        {
            Salt                = salt,
            Iterations          = iterations,
            MemorySize          = memoryKiB,
            DegreeOfParallelism = parallelism
        };
        return argon2.GetBytes(hashLength);
    }

    private static string Encode(int iterations, int memoryKiB, int parallelism, byte[] salt, byte[] hash)
    {
        var saltB64 = Base64NoPad(salt);
        var hashB64 = Base64NoPad(hash);
        return $"$argon2id$v={ArgonVersion}$m={memoryKiB},t={iterations},p={parallelism}${saltB64}${hashB64}";
    }

    private static bool TryDecode(
        string     encoded,
        out int    iterations,
        out int    memoryKiB,
        out int    parallelism,
        out byte[] salt,
        out byte[] hash)
    {
        iterations  = 0;
        memoryKiB   = 0;
        parallelism = 0;
        salt        = [];
        hash        = [];

        var parts = encoded.Split('$');
        if (parts.Length != 6 || parts[0].Length != 0) return false;
        if (parts[1] != "argon2id") return false;
        if (!parts[2].StartsWith("v=", StringComparison.Ordinal)) return false;
        if (!int.TryParse(parts[2][2..], out var version) || version != ArgonVersion) return false;

        var paramDict = new Dictionary<string, int>(3);
        foreach (var kv in parts[3].Split(','))
        {
            var eq = kv.IndexOf('=');
            if (eq <= 0) return false;
            var key = kv[..eq];
            if (!int.TryParse(kv[(eq + 1)..], out var value)) return false;
            paramDict[key] = value;
        }

        if (!paramDict.TryGetValue("m", out memoryKiB)   || memoryKiB   <= 0) return false;
        if (!paramDict.TryGetValue("t", out iterations)  || iterations  <= 0) return false;
        if (!paramDict.TryGetValue("p", out parallelism) || parallelism <= 0) return false;

        try
        {
            salt = Base64FromNoPad(parts[4]);
            hash = Base64FromNoPad(parts[5]);
        }
        catch (FormatException)
        {
            return false;
        }

        return salt.Length > 0 && hash.Length > 0;
    }

    private static string Base64NoPad(byte[] bytes)
    {
        return Convert.ToBase64String(bytes).TrimEnd('=');
    }

    private static byte[] Base64FromNoPad(string value)
    {
        var padding = (4 - value.Length % 4) % 4;
        return Convert.FromBase64String(value + new string('=', padding));
    }
}
