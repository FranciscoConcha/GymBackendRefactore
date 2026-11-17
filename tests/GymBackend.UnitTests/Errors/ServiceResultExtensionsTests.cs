using System.Text.Json;
using GymBackend.Application.Common;
using GymBackend.DTOs;
using GymBackend.Extensions;
using Microsoft.AspNetCore.Mvc;

namespace GymBackend.UnitTests.Errors;

public sealed class ServiceResultExtensionsTests
{
    [Fact]
    public void Conflict_Returns409WithMessage()
    {
        var controller = new TestController();
        var result = Assert.IsType<ConflictObjectResult>(controller.ToActionResult(
            ServiceResult<string>.Failure(ServiceError.Conflict("Concurrent conflict"))));
        Assert.Equal(409, result.StatusCode);
        var body = Assert.IsType<MessageResponse>(result.Value);
        var json = JsonSerializer.SerializeToElement(body, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        Assert.Equal("Concurrent conflict", json.GetProperty("message").GetString());
        Assert.Single(json.EnumerateObject());
    }

    private sealed class TestController : ControllerBase { }
}
