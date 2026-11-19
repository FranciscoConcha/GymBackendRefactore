using GymBackend.DTOs;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace GymBackend.Security;

[AttributeUsage(AttributeTargets.Method)]
public sealed class AllowPendingPasswordChangeAttribute : Attribute;

public sealed class PendingPasswordChangeFilter : IAuthorizationFilter
{
    public const string ClaimType = "gym:pending-password-change";

    public void OnAuthorization(AuthorizationFilterContext context)
    {
        if (context.HttpContext.User.Identity?.IsAuthenticated == true
            && context.ActionDescriptor.EndpointMetadata.OfType<Microsoft.AspNetCore.Authorization.IAuthorizeData>().Any()
            && context.HttpContext.User.HasClaim(ClaimType, "true")
            && !context.ActionDescriptor.EndpointMetadata.OfType<AllowPendingPasswordChangeAttribute>().Any())
            context.Result = new ObjectResult(new MessageResponse("Debe cambiar su contrase\u00f1a temporal antes de continuar."))
            { StatusCode = StatusCodes.Status403Forbidden };
    }
}
