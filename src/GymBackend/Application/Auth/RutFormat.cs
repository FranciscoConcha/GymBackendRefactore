using System.Text.RegularExpressions;

namespace GymBackend.Application.Auth;

public static class RutFormat
{
    public const string ValidationMessage = "El RUT debe tener un cuerpo de 7 u 8 dígitos y un dígito verificador válido (0-9 o K).";

    public static bool IsValid(string? rut) => TryNormalize(rut, out _);

    public static bool TryNormalize(string? input, out string canonical)
    {
        canonical = string.Empty;
        if (input is null) return false;
        var rut = input.Trim();
        if (!Regex.IsMatch(rut, @"\A(?:[0-9]{7,8}|[0-9]{1,2}\.[0-9]{3}\.[0-9]{3})-?[0-9Kk]\z"))
            return false;
        rut = rut.Replace(".", "").Replace("-", "").ToUpperInvariant();
        var body = rut[..^1];
        if (ComputeCheckDigit(body) != rut[^1].ToString()) return false;
        canonical = $"{body}-{rut[^1]}";
        return true;
    }

    public static string ComputeCheckDigit(string body)
    {
        if (body is null || !Regex.IsMatch(body, @"\A[0-9]{7,8}\z"))
            throw new ArgumentException("El cuerpo del RUT debe tener 7 u 8 dígitos.", nameof(body));
        var sum = 0;
        var factor = 2;
        for (var index = body.Length - 1; index >= 0; index--)
        {
            sum += (body[index] - '0') * factor;
            factor = factor == 7 ? 2 : factor + 1;
        }
        var digit = 11 - sum % 11;
        return digit switch { 11 => "0", 10 => "K", _ => digit.ToString(System.Globalization.CultureInfo.InvariantCulture) };
    }
}
