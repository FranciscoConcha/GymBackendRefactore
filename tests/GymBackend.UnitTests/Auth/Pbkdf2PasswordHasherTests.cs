using GymBackend.Application.Auth;
using GymBackend.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace GymBackend.UnitTests.Auth;

public sealed class Pbkdf2PasswordHasherTests
{
    private readonly Pbkdf2PasswordHasher hasher = new();

    [Fact]
    public void Hash_UsesSaltAndPbkdf2()
    {
        var hash = hasher.Hash("secreto");
        Assert.NotEqual(32, Convert.FromBase64String(hash).Length);
        Assert.NotEqual(hash, hasher.Hash("secreto"));
        Assert.Equal(PasswordCheck.Success, hasher.Verify("secreto", hash));
        Assert.Equal(PasswordCheck.Failed, hasher.Verify("incorrecta", hash));
    }

    [Fact]
    public void Verify_LegacyHash_RequiresRehash()
    {
        const string hash = "33M2Vik6GcVPaQk7qRbwoaKjwVH8lcE/OnlMJjHus6Y=";
        Assert.Equal(PasswordCheck.SuccessRehashNeeded, hasher.Verify("secreto", hash));
        Assert.Equal(PasswordCheck.Failed, hasher.Verify("incorrecta", hash));
    }

    [Fact]
    public void Verify_OldIdentityHash_RequiresRehash()
    {
        var oldHasher = new PasswordHasher<User>(Microsoft.Extensions.Options.Options.Create(new PasswordHasherOptions
        {
            CompatibilityMode = PasswordHasherCompatibilityMode.IdentityV2,
        }));
        var hash = oldHasher.HashPassword(new User(), "secreto");
        Assert.Equal(PasswordCheck.SuccessRehashNeeded, hasher.Verify("secreto", hash));
    }

    [Theory]
    [InlineData("")]
    [InlineData("invalid")]
    [InlineData("AA==")]
    public void Verify_InvalidHash_Fails(string hash) =>
        Assert.Equal(PasswordCheck.Failed, hasher.Verify("secreto", hash));

    [Fact]
    public void VerifyDummy_DoesNotThrow() => hasher.VerifyDummy("secreto");
}
