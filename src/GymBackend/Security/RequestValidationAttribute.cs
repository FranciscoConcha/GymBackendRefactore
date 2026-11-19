using GymBackend.DTOs;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace GymBackend.Security;

[AttributeUsage(AttributeTargets.Class)]
public sealed class RequestValidationAttribute : Attribute, IAsyncActionFilter, IOrderedFilter
{
    public int Order => -2500;

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        if (!context.ModelState.IsValid)
        {
            context.Result = new BadRequestObjectResult(new MessageResponse("La solicitud contiene datos inválidos."));
            return;
        }
        await next();
    }
}
