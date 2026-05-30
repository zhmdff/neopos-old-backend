using BusinessLayer.Utilities;

namespace NeoPos.Tests;

public class PasswordHashHelperTests
{
    [Fact]
    public void Hash_ProducesBcryptHash_VerifySucceeds()
    {
        var hash = PasswordHashHelper.Hash("Secret123");
        Assert.True(PasswordHashHelper.IsBcryptHash(hash));
        Assert.True(PasswordHashHelper.Verify("Secret123", hash));
        Assert.False(PasswordHashHelper.Verify("Wrong", hash));
    }

    [Fact]
    public void Hash_TrimsPassword_BeforeHashing()
    {
        var hash = PasswordHashHelper.Hash("  abc  ");
        Assert.True(PasswordHashHelper.Verify("abc", hash));
    }

    [Fact]
    public void Hash_EmptyPassword_Throws()
    {
        Assert.Throws<ArgumentException>(() => PasswordHashHelper.Hash(""));
        Assert.Throws<ArgumentException>(() => PasswordHashHelper.Hash("   "));
    }

    [Fact]
    public void Verify_LegacyPlaintext_MatchesExactly()
    {
        Assert.True(PasswordHashHelper.Verify("plain", "plain"));
        Assert.False(PasswordHashHelper.Verify("plain", "other"));
        Assert.False(PasswordHashHelper.IsBcryptHash("plain"));
    }

    [Fact]
    public void Verify_EmptyStored_ReturnsFalse()
    {
        Assert.False(PasswordHashHelper.Verify("x", null));
        Assert.False(PasswordHashHelper.Verify("x", ""));
    }

    [Fact]
    public void NormalizeToBcrypt_UpgradesPlaintext()
    {
        var bcrypt = PasswordHashHelper.NormalizeToBcrypt("plainPass");
        Assert.True(PasswordHashHelper.IsBcryptHash(bcrypt));
        Assert.True(PasswordHashHelper.Verify("plainPass", bcrypt));

        var already = PasswordHashHelper.Hash("x");
        Assert.Equal(already, PasswordHashHelper.NormalizeToBcrypt(already));
    }

    [Fact]
    public void Verify_InvalidBcrypt_ReturnsFalse_DoesNotThrow()
    {
        Assert.False(PasswordHashHelper.Verify("x", "$2a$not-a-valid-bcrypt-hash-value"));
    }
}
