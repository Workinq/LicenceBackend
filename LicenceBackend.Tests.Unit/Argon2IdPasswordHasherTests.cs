using LicenceBackend.Infrastructure.Crypto;

namespace LicenceBackend.Tests.Unit;

public sealed class Argon2IdPasswordHasherTests
{
    [Fact]
    public void Hash_then_Verify_with_same_password_returns_true()
    {
        var hasher = new Argon2IdPasswordHasher();
        var hash = hasher.Hash("correct horse battery staple");
        Assert.True(hasher.Verify("correct horse battery staple", hash));
    }

    [Fact]
    public void Verify_with_wrong_password_returns_false()
    {
        var hasher = new Argon2IdPasswordHasher();
        var hash = hasher.Hash("correct horse battery staple");
        Assert.False(hasher.Verify("wrong-password", hash));
    }

    [Fact]
    public void Hash_produces_different_strings_for_same_input_due_to_random_salt()
    {
        var hasher = new Argon2IdPasswordHasher();
        var first = hasher.Hash("same-password");
        var second = hasher.Hash("same-password");
        Assert.NotEqual(first, second);
        Assert.True(hasher.Verify("same-password", first));
        Assert.True(hasher.Verify("same-password", second));
    }

    [Fact]
    public void Verify_with_empty_password_returns_false_without_throwing()
    {
        var hasher = new Argon2IdPasswordHasher();
        var hash = hasher.Hash("real-password-12345");
        Assert.False(hasher.Verify("", hash));
    }

    [Fact]
    public void Verify_with_malformed_encoded_hash_returns_false()
    {
        var hasher = new Argon2IdPasswordHasher();
        Assert.False(hasher.Verify("any-password", "not-a-valid-phc-string"));
    }

    [Fact]
    public void VerifyDummy_does_not_throw_for_any_input_shape()
    {
        var hasher = new Argon2IdPasswordHasher();
        hasher.VerifyDummy("anything");
        hasher.VerifyDummy("");
    }
}
