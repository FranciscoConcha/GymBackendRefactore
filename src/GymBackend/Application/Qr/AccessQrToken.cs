using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.WebUtilities;

namespace GymBackend.Application.Qr;

public sealed record AccessQrPayload(
    [property: JsonPropertyName("v")] int Version,
    [property: JsonPropertyName("k")] string Kind,
    [property: JsonPropertyName("rid"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] int? ReservationId,
    [property: JsonPropertyName("gid"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] int? GroupBookingId,
    [property: JsonPropertyName("uid")] int UserId,
    [property: JsonPropertyName("nbf")] long NotBefore,
    [property: JsonPropertyName("exp")] long Expires)
{
    public bool IsCurrent(DateTimeOffset now) => NotBefore <= now.ToUnixTimeSeconds() && now.ToUnixTimeSeconds() < Expires;
}

public static class AccessQrToken
{
    public const string Invalid = "Código QR inválido.";
    public const string NotCurrent = "El código QR no está vigente para este bloque.";

    public static string Create(AccessQrPayload payload, string key)
    {
        var bytes = JsonSerializer.SerializeToUtf8Bytes(payload);
        return $"{WebEncoders.Base64UrlEncode(bytes)}.{WebEncoders.Base64UrlEncode(HMACSHA256.HashData(Encoding.UTF8.GetBytes(key), bytes))}";
    }

    public static bool TryRead(string? token, string key, out AccessQrPayload payload)
    {
        payload = null!;
        if (string.IsNullOrWhiteSpace(token) || token.Length > 4096) return false;
        var parts = token.Split('.');
        if (parts.Length != 2 || parts.Any(p => p.Length == 0 || p.Any(c => !char.IsAsciiLetterOrDigit(c) && c is not '-' and not '_'))) return false;
        try
        {
            var bytes = WebEncoders.Base64UrlDecode(parts[0]);
            var signature = WebEncoders.Base64UrlDecode(parts[1]);
            if (!CryptographicOperations.FixedTimeEquals(signature, HMACSHA256.HashData(Encoding.UTF8.GetBytes(key), bytes))) return false;
            using var json = JsonDocument.Parse(bytes);
            var root = json.RootElement;
            if (root.ValueKind != JsonValueKind.Object) return false;
            var names = root.EnumerateObject().Select(p => p.Name).ToArray();
            if (names.Distinct(StringComparer.Ordinal).Count() != names.Length) return false;
            var value = JsonSerializer.Deserialize<AccessQrPayload>(bytes);
            if (value is null || value.Version != 2 || value.UserId <= 0 || value.NotBefore <= 0 || value.Expires <= value.NotBefore) return false;
            var expected = value.Kind == "R" ? new[] { "v", "k", "rid", "uid", "nbf", "exp" } : new[] { "v", "k", "gid", "uid", "nbf", "exp" };
            if (names.Length != expected.Length || expected.Except(names, StringComparer.Ordinal).Any()) return false;
            if (!(value.Kind == "R" && value.ReservationId > 0 && value.GroupBookingId is null ||
                value.Kind == "G" && value.GroupBookingId > 0 && value.ReservationId is null)) return false;
            payload = value;
            return true;
        }
        catch (Exception e) when (e is FormatException or JsonException or ArgumentException or OverflowException)
        {
            return false;
        }
    }
}
