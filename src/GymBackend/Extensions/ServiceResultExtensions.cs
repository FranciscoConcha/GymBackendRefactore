using GymBackend.Application.Common;
using GymBackend.DTOs;
using Microsoft.AspNetCore.Mvc;

namespace GymBackend.Extensions;

public static class ServiceResultExtensions
{
    /// <summary>Traduce un resultado de servicio a la respuesta HTTP vigente (cuerpos <c>{ message }</c>; 403 sin cuerpo).</summary>
    public static IActionResult ToActionResult<T>(this ControllerBase controller, ServiceResult<T> result) =>
        result.IsSuccess ? controller.Ok(result.Value) : controller.ToActionResult(result.Error!);

    public static IActionResult ToActionResult(this ControllerBase controller, ServiceError error) => error.Kind switch
    {
        ServiceErrorKind.Validation => controller.BadRequest(new MessageResponse(error.Message)),
        ServiceErrorKind.Unauthorized => controller.Unauthorized(new MessageResponse(error.Message)),
        ServiceErrorKind.Forbidden => controller.Forbid(),
        ServiceErrorKind.Conflict => controller.Conflict(new MessageResponse(error.Message)),
        ServiceErrorKind.NotFound => controller.NotFound(new MessageResponse(error.Message)),
        _ => throw new ArgumentOutOfRangeException(nameof(error)),
    };
}
