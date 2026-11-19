using System.Globalization;
using System.Security.Cryptography;
using GymBackend.Application.Auth;

namespace GymBackend.IntegrationTests.Infrastructure;

public static class TestData
{
    private static int _rut = RandomNumberGenerator.GetInt32(20_000_000, 89_000_001);

    public static string NextRut()
    {
        while (true)
        {
            var rut = Interlocked.Increment(ref _rut);
            if (rut > 99_999_999)
                throw new InvalidOperationException("Se agotaron los RUT de ocho dígitos de esta ejecución.");

            var body = rut.ToString(CultureInfo.InvariantCulture);
            var digit = RutFormat.ComputeCheckDigit(body);
            // DV K se omite: los correos derivados del RUT se guardan en minúsculas y las pruebas los comparan literalmente.
            // La normalización de K se prueba en RutFormatTests y AuthTests.
            if (digit != "K")
                return $"{body}-{digit}";
        }
    }
}
