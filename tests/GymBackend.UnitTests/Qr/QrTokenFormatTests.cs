using System.Text;
using GymBackend.Application.Qr;

namespace GymBackend.UnitTests.Qr;

public sealed class QrTokenFormatTests
{
    private static readonly DateOnly Today = new(2026, 11, 17);
    private const string InvalidCode = "Código QR inválido.";

    [Fact]
    public void Create_UsesExistingPayloadAndUppercaseHexSignature()
    {
        var token = QrTokenFormat.Create("12345678", "k", Today);
        var prefix = Convert.ToBase64String(Encoding.UTF8.GetBytes("12345678|20261117")) + ".";
        Assert.StartsWith(prefix, token);
        Assert.Matches("^[0-9A-F]{64}$", token[prefix.Length..]);
    }

    [Fact]
    public void Create_ThenValidate_ReturnsRut()
    {
        var token = QrTokenFormat.Create("12345678", "k", Today);
        Assert.True(QrTokenFormat.TryValidate(token, "k", Today, out var rut, out var error));
        Assert.Equal("12345678", rut);
        Assert.Equal(string.Empty, error);
    }

    [Fact]
    public void TryValidate_AlteredSignature_IsInvalid()
    {
        var token = QrTokenFormat.Create("12345678", "k", Today);
        var altered = token[..^1] + (token[^1] == '0' ? "1" : "0");
        AssertInvalid(altered, "k", Today, InvalidCode);
    }

    [Fact]
    public void TryValidate_OtherKey_IsInvalid()
    {
        AssertInvalid(QrTokenFormat.Create("12345678", "k", Today), "other", Today, InvalidCode);
    }

    [Fact]
    public void TryValidate_OtherDate_ReturnsDateMessage()
    {
        AssertInvalid(QrTokenFormat.Create("12345678", "k", Today.AddDays(-1)), "k", Today,
            "El código QR no corresponde al día de hoy.");
    }

    [Theory]
    [InlineData("%%%.ABCD")]
    [InlineData("without-separator")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void TryValidate_MalformedToken_IsInvalid(string? token)
    {
        AssertInvalid(token, "k", Today, InvalidCode);
    }

    private static void AssertInvalid(string? token, string key, DateOnly date, string expectedError)
    {
        Assert.False(QrTokenFormat.TryValidate(token, key, date, out var rut, out var error));
        Assert.Equal(string.Empty, rut);
        Assert.Equal(expectedError, error);
    }
}
