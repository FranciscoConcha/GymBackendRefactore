using GymBackend.Application.Excel;
using GymBackend.Models;

namespace GymBackend.UnitTests.Excel;

public sealed class UserImportRowsTests
{
    private static UserImportRow Valid(int row = 2) => new(row, "12.345.678-5", " Ana ", " Pérez ", " ANA@EXAMPLE.TEST ", "Estudiante", "");

    [Fact]
    public void NormalizesHeadersAndIdentifiers()
    {
        Assert.Equal("categoria", UserImportRows.NormalizeHeader(" CATEGORÍA "));
        var row = Assert.Single(UserImportRows.Validate([Valid()], [], new HashSet<string>(), new HashSet<string>()));
        Assert.Equal("Crear", row.Action);
        Assert.Equal("12345678-5", row.Rut);
        Assert.Equal("ana@example.test", row.Email);
        Assert.Equal("Ana", row.FirstName);
    }

    [Fact]
    public void DuplicateRowsAreAllErrors()
    {
        var rows = UserImportRows.Validate([Valid(), Valid(3)], [], new HashSet<string>(), new HashSet<string>());
        Assert.All(rows, r => Assert.Equal("Error", r.Action));
    }

    [Fact]
    public void ExistingIdentifiersAreSkipped()
    {
        Assert.Equal("Omitir", Assert.Single(UserImportRows.Validate([Valid()], [], new HashSet<string> { "12345678-5" }, new HashSet<string>())).Action);
        Assert.Equal("Omitir", Assert.Single(UserImportRows.Validate([Valid()], [], new HashSet<string>(), new HashSet<string> { "ana@example.test" })).Action);
    }

    [Theory]
    [InlineData("Admin", "", false)]
    [InlineData("Deportista", "", false)]
    [InlineData("Estudiante", "Fútbol", false)]
    [InlineData("Deportista", "Fútbol", false)]
    public void InvalidIdentityIsRejected(string category, string branch, bool enabled)
    {
        var branches = new[] { new SportBranch { Id = 1, Name = "Fútbol", Enabled = enabled } };
        var row = Assert.Single(UserImportRows.Validate([Valid() with { Category = category, Branch = branch }], branches, new HashSet<string>(), new HashSet<string>()));
        Assert.Equal("Error", row.Action);
    }

    [Fact]
    public void EnabledAthleteBranchIsAcceptedAndFormulaIsRejected()
    {
        SportBranch[] branches = [new() { Id = 1, Name = "Fútbol", Enabled = true }];
        var athlete = Valid() with { Category = "Deportista", Branch = "Fútbol" };
        Assert.Equal("Crear", Assert.Single(UserImportRows.Validate([athlete], branches, new HashSet<string>(), new HashSet<string>())).Action);
        Assert.Equal("Error", Assert.Single(UserImportRows.Validate([athlete with { HasFormula = true }], branches, new HashSet<string>(), new HashSet<string>())).Action);
    }
}
