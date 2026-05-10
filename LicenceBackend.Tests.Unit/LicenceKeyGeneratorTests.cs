using LicenceBackend.Infrastructure.Crypto;

namespace LicenceBackend.Tests.Unit;

public sealed class LicenceKeyGeneratorTests
{
    private const string CrockfordAlphabet = "0123456789ABCDEFGHJKMNPQRSTVWXYZ";

    [Fact]
    public void Generate_returns_LIC_dash_prefixed_five_groups_of_five()
    {
        var key = new LicenceKeyGenerator().Generate();

        Assert.StartsWith("LIC-", key);
        var groups = key.Split('-');
        Assert.Equal(6,     groups.Length);
        Assert.Equal("LIC", groups[0]);
        for (var i = 1; i < groups.Length; i++) Assert.Equal(5, groups[i].Length);
    }

    [Fact]
    public void Generate_only_uses_crockford_alphabet_chars()
    {
        var generator = new LicenceKeyGenerator();
        for (var i = 0; i < 200; i++)
        {
            var key = generator.Generate();
            foreach (var c in key.AsSpan(4))
            {
                if (c == '-') continue;
                Assert.Contains(c, CrockfordAlphabet);
            }
        }
    }

    [Fact]
    public void Generate_produces_distinct_keys_across_a_thousand_iterations()
    {
        var generator = new LicenceKeyGenerator();
        var seen      = new HashSet<string>();
        for (var i = 0; i < 1000; i++) Assert.True(seen.Add(generator.Generate()), "Duplicate licence key generated.");
    }
}
