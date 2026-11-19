using GymBackend.Data;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace GymBackend.UnitTests.Errors;

public sealed class DbUpdateExceptionExtensionsTests
{
    [Theory]
    [InlineData("23505", true)]
    [InlineData("23503", false)]
    public void OnlyUniqueViolation_IsRecognized(string sqlState, bool expected)
    {
        var exception = new DbUpdateException("Database failure",
            new PostgresException("Database failure", "ERROR", "ERROR", sqlState));
        Assert.Equal(expected, exception.IsUniqueViolation());
    }

    [Fact]
    public void OtherInnerException_IsNotUniqueViolation()
    {
        Assert.False(new DbUpdateException("Database failure", new InvalidOperationException()).IsUniqueViolation());
    }
}
