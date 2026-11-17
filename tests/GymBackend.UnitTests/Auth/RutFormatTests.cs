using GymBackend.Application.Auth;

namespace GymBackend.UnitTests.Auth;

public sealed class RutFormatTests
{
    [Theory]
    [InlineData("12345678", true)]
    [InlineData("123456789", true)]
    [InlineData("1234567", false)]
    [InlineData("1234567890", false)]
    [InlineData("12.345.678-9", false)]
    [InlineData("12.345.678", false)]
    [InlineData("12345678-9", false)]
    [InlineData("abcdefgh", false)]
    [InlineData("", false)]
    public void IsValid_PreservesLegacyFormat(string rut, bool expected)
    {
        Assert.Equal(expected, RutFormat.IsValid(rut));
    }
}
