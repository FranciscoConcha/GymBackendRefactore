using System.Security.Cryptography;
using System.Text;

namespace GymBackend.Application.Auth;

// D1: esquema heredado, se reemplaza en F5.
public sealed class LegacySha256PasswordHasher : IUserPasswordHasher
{
    public string Hash(string password) => Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(password)));

    public bool Verify(string password, string hash) =>
        CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(Hash(password)), Encoding.UTF8.GetBytes(hash));
}
