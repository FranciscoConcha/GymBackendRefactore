using System.Globalization;
using GymBackend.Application.Auth;
using System.Security.Cryptography;

namespace GymBackend.IntegrationTests.Infrastructure;

public static class TestData
{
    private static int _rut = RandomNumberGenerator.GetInt32(20_000_000, 89_000_001);

    public static string NextRut()
    {
        var rut = Interlocked.Increment(ref _rut);
        if (rut > 99_999_999)
            throw new InvalidOperationException("Se agotaron los RUT de ocho dígitos de esta ejecución.");

        var body = rut.ToString(CultureInfo.InvariantCulture);
        return $"{body}-{RutFormat.ComputeCheckDigit(body)}";
    }
}
