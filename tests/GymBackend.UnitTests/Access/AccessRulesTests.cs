using GymBackend.Application.Access;

namespace GymBackend.UnitTests.Access;

public sealed class AccessRulesTests
{
    [Theory]
    [InlineData(9, 44, 59, false)]
    [InlineData(9, 45, 0, true)]
    [InlineData(10, 59, 59, true)]
    [InlineData(11, 0, 0, false)]
    public void Window_IncludesFifteenMinutesBeforeAndExcludesEnd(int hour, int minute, int second, bool expected) =>
        Assert.Equal(expected, AccessRules.IsWithin(new(10, 0), new(11, 0), new(hour, minute, second)));

    [Theory]
    [InlineData("Deportista", 1, 1, 2, 1, false, true)]
    [InlineData("Deportista", 2, 1, 2, 1, false, false)]
    [InlineData("Estudiante", 1, 1, 2, 1, false, false)]
    [InlineData("Deportista", 1, 1, 2, 2, false, false)]
    [InlineData("Deportista", 1, 1, 2, 2, true, true)]
    public void Group_RequiresBranchAndDistinctSeatOrPriorUse(string category, int branch, int groupBranch,
        int seats, int used, bool priorUse, bool expected) =>
        Assert.Equal(expected, AccessRules.CanUseGroup(category, branch, groupBranch, seats, used, priorUse));

    [Fact]
    public void Occupancy_ThirtiethPersonCanEnterButThirtyFirstCannot()
    {
        Assert.Null(AccessRules.Rejection(true, true, false, true, 29, 30));
        Assert.Equal(AccessRules.Full, AccessRules.Rejection(true, true, false, true, 30, 30));
        Assert.NotNull(AccessRules.Rejection(false, true, false, true, 0, 30));
        Assert.NotNull(AccessRules.Rejection(true, false, false, true, 0, 30));
        Assert.NotNull(AccessRules.Rejection(true, true, true, true, 0, 30));
        Assert.Equal(AccessRules.NoSession, AccessRules.Rejection(true, true, false, false, 0, 30));
    }

    [Theory]
    [InlineData(null, false)]
    [InlineData("", false)]
    [InlineData("Pasaporte", false)]
    [InlineData("CarnetIdentidad", true)]
    [InlineData("TNE", true)]
    [InlineData("CredencialUniversitaria", true)]
    public void Document_RequiresVisualConfirmation(string? document, bool expected) => Assert.Equal(expected, AccessRules.ValidDocument(document));
}
