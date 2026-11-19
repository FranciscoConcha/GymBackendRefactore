using GymBackend.Application.Auth;

namespace GymBackend.UnitTests.Auth;

public sealed class RutFormatTests
{
    [Theory]
    [InlineData("12345678", "5")]
    [InlineData("10000013", "K")]
    [InlineData("10000004", "0")]
    [InlineData("1234567", "4")]
    public void ComputeCheckDigit_Modulo11(string body, string expected) =>
        Assert.Equal(expected, RutFormat.ComputeCheckDigit(body));

    [Theory]
    [InlineData("12.345.678-5", "12345678-5")]
    [InlineData("12345678-5", "12345678-5")]
    [InlineData("123456785", "12345678-5")]
    [InlineData("12.345.6785", "12345678-5")]
    [InlineData("10.000.013-k", "10000013-K")]
    [InlineData("1.234.567-4", "1234567-4")]
    public void TryNormalize_ValidInput(string input, string expected)
    {
        Assert.True(RutFormat.TryNormalize(input, out var canonical));
        Assert.Equal(expected, canonical);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("12345678-9")]
    [InlineData("123456-0")]
    [InlineData("123456789-0")]
    [InlineData("abcdefgh-K")]
    [InlineData("1234567A-5")]
    [InlineData("12..345.678-5")]
    [InlineData("12345678--5")]
    public void TryNormalize_InvalidInput(string? input)
    {
        Assert.False(RutFormat.TryNormalize(input, out var canonical));
        Assert.Equal(string.Empty, canonical);
    }

    [Theory]
    [InlineData("")]
    [InlineData("123")]
    [InlineData("abcdefgh")]
    public void ComputeCheckDigit_InvalidBody(string body) =>
        Assert.Throws<ArgumentException>(() => RutFormat.ComputeCheckDigit(body));
}
