using GymBackend.Application.Auth;
using GymBackend.Domain;
namespace GymBackend.UnitTests.Auth;

public sealed class IdentityRulesTests
{
    [Theory]
    [InlineData(Roles.Usuario, UserCategories.Deportista, 1, true, "", true)]
    [InlineData(Roles.Usuario, UserCategories.Deportista, null, false, "", false)]
    [InlineData(Roles.Usuario, UserCategories.Deportista, 1, false, "", false)]
    [InlineData(Roles.Usuario, UserCategories.Estudiante, null, false, "", true)]
    [InlineData(Roles.Usuario, UserCategories.Funcionario, null, false, "", true)]
    [InlineData(Roles.Usuario, UserCategories.Externo, null, false, "", true)]
    [InlineData(Roles.Usuario, UserCategories.Estudiante, 1, true, "", false)]
    [InlineData(Roles.Usuario, UserCategories.Funcionario, 1, true, "", false)]
    [InlineData(Roles.Usuario, UserCategories.Externo, 1, true, "", false)]
    [InlineData(Roles.Usuario, null, null, false, "", false)]
    [InlineData(Roles.Usuario, "Unknown", null, false, "", false)]
    [InlineData(Roles.Recepcionista, null, null, false, "reception", true)]
    [InlineData(Roles.Recepcionista, null, null, false, "", false)]
    [InlineData(Roles.Recepcionista, UserCategories.Estudiante, null, false, "reception", false)]
    [InlineData(Roles.Recepcionista, null, 1, true, "reception", false)]
    [InlineData(Roles.Admin, null, null, false, "admin", true)]
    [InlineData(Roles.Admin, UserCategories.Externo, null, false, "admin", false)]
    [InlineData("Unknown", null, null, false, "", false)]
    public void Validate_IdentityCoherence(string role, string? category, int? branchId, bool enabled, string username, bool valid) =>
        Assert.Equal(valid, IdentityRules.Validate(role, category, branchId, enabled, username) is null);

    [Fact]
    public void Staff_UsernameLengthIsLimited() =>
        Assert.NotNull(IdentityRules.Validate(Roles.Recepcionista, null, null, false, new string('a', 65)));
}
