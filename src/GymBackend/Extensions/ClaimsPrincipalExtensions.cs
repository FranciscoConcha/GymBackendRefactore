using System.Security.Claims;
using GymBackend.Domain;

namespace GymBackend.Extensions;

public static class ClaimsPrincipalExtensions
{
    public static int? GetUserId(this ClaimsPrincipal principal) =>
        int.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var userId) ? userId : null;

    public static string? GetRut(this ClaimsPrincipal principal) =>
        principal.FindFirstValue(GymClaimTypes.Rut);
}
