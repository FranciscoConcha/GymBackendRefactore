using GymBackend.Models;
using Microsoft.AspNetCore.Identity;

namespace GymBackend.Application.Auth;

public sealed class Pbkdf2PasswordHasher : IUserPasswordHasher
{
    private static readonly Lazy<string> DummyHash = new(() =>
        new PasswordHasher<User>().HashPassword(new User(), Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32))));

    private readonly PasswordHasher<User> hasher = new();
    private readonly User hashingUser = new();

    public string Hash(string password) => hasher.HashPassword(hashingUser, password);

    public PasswordCheck Verify(string password, string hash)
    {
        byte[] decoded;
        try { decoded = Convert.FromBase64String(hash); }
        catch (FormatException) { return PasswordCheck.Failed; }

        if (decoded.Length == 32)
            return new LegacySha256PasswordHasher().Verify(password, hash)
                ? PasswordCheck.SuccessRehashNeeded : PasswordCheck.Failed;

        return hasher.VerifyHashedPassword(hashingUser, hash, password) switch
        {
            PasswordVerificationResult.Success => PasswordCheck.Success,
            PasswordVerificationResult.SuccessRehashNeeded => PasswordCheck.SuccessRehashNeeded,
            _ => PasswordCheck.Failed,
        };
    }

    public void VerifyDummy(string password) => _ = hasher.VerifyHashedPassword(hashingUser, DummyHash.Value, password);
}
