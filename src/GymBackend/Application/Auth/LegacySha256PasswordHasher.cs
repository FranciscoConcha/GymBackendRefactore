using System.Security.Cryptography;
using System.Text;

namespace GymBackend.Application.Auth;

// Solo para verificar contraseñas almacenadas con el esquema heredado.
public sealed class LegacySha256PasswordHasher
{
    public bool Verify(string password, string hash)
    {
        Span<byte> stored = stackalloc byte[32];
        return Convert.TryFromBase64String(hash, stored, out var length) && length == 32 &&
            CryptographicOperations.FixedTimeEquals(SHA256.HashData(Encoding.UTF8.GetBytes(password)), stored);
    }
}
