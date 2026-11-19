using GymBackend.Application.Auth;

namespace GymBackend.UnitTests.Auth;

public sealed class LegacySha256PasswordHasherTests
{
    private const string KnownHash = "33M2Vik6GcVPaQk7qRbwoaKjwVH8lcE/OnlMJjHus6Y=";

    [Theory]
    [InlineData("secreto", true)]
    [InlineData("incorrecta", false)]
    [InlineData("", false)]
    public void Verify_ChecksPassword(string password, bool expected)
    {
        Assert.Equal(expected, new LegacySha256PasswordHasher().Verify(password, KnownHash));
    }

    [Theory]
    [InlineData("")]
    [InlineData("invalid")]
    public void Verify_InvalidHash_ReturnsFalse(string hash)
    {
        Assert.False(new LegacySha256PasswordHasher().Verify("secreto", hash));
    }
}
