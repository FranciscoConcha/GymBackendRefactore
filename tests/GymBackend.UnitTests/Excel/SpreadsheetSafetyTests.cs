using GymBackend.Application.Excel;

namespace GymBackend.UnitTests.Excel;

public sealed class SpreadsheetSafetyTests
{
    [Theory]
    [InlineData("=SUM(A1:A2)")]
    [InlineData("+1")]
    [InlineData("-1")]
    [InlineData("@test")]
    [InlineData("\tformula")]
    [InlineData("\rformula")]
    public void DangerousPrefixesAreNeutralized(string value) => Assert.Equal("'" + value, SpreadsheetSafety.Neutralize(value));

    [Theory]
    [InlineData("")]
    [InlineData("Ana")]
    [InlineData("'safe")]
    public void SafeValuesArePreserved(string value) => Assert.Equal(value, SpreadsheetSafety.Neutralize(value));
}
