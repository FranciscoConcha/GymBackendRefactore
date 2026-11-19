using System.Security.Claims;
using GymBackend.Application.Auth;
using GymBackend.Application.Common;
using GymBackend.Domain;
using GymBackend.DTOs;
using GymBackend.Extensions;
using GymBackend.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace GymBackend.Controllers;

[ApiController]
[Route(ApiRoutes.V1)]
public class AuthController(IAuthService authService, IUserService userService) : ControllerBase
{
    [HttpPost("register")]
    [Authorize(Roles = Roles.Admin)]
    [ProducesResponseType<UserMessageResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<MessageResponse>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Register(RegisterRequest request, CancellationToken ct = default)
    {
        return this.ToActionResult(await userService.RegisterAsync(request, ct));
    }

    [HttpPost("login")]
    [EnableRateLimiting(RateLimitingExtensions.LoginPolicy)]
    [ProducesResponseType<AuthResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<MessageResponse>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<MessageResponse>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<MessageResponse>(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Login(LoginRequest request, CancellationToken ct = default)
    {
        return CompleteLogin(await authService.LoginAsync(request, ct));
    }

    [HttpPost("admin-login")]
    [EnableRateLimiting(RateLimitingExtensions.LoginPolicy)]
    [ProducesResponseType<AuthResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<MessageResponse>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<MessageResponse>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<MessageResponse>(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> AdminLogin(AdminLoginRequest request, CancellationToken ct = default)
    {
        return CompleteLogin(await authService.AdminLoginAsync(request, ct));
    }

    private IActionResult CompleteLogin(ServiceResult<AuthSession> result)
    {
        if (!result.IsSuccess) return this.ToActionResult(result.Error!);
        var session = result.Value!;
        SessionCookie.Append(Response, session.Token.Value, session.Token.ExpiresAt);
        return Ok(new AuthResponse(session.Message, session.User, session.Token.ExpiresAt));
    }

    [HttpPost("logout")]
    [AllowAnonymous]
    [ProducesResponseType<MessageResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<MessageResponse>(StatusCodes.Status403Forbidden)]
    [AllowPendingPasswordChange]
    public async Task<IActionResult> Logout(CancellationToken ct = default)
    {
        if (User.Identity?.IsAuthenticated == true && int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id))
            await authService.LogoutAsync(id, ct);
        SessionCookie.Delete(Response);
        return Ok(new MessageResponse("Sesión cerrada."));
    }

    [HttpPost("change-password")]
    [Authorize]
    [AllowPendingPasswordChange]
    [EnableRateLimiting(RateLimitingExtensions.LoginPolicy)]
    public async Task<IActionResult> ChangePassword(ChangePasswordRequest request, CancellationToken ct = default)
    {
        if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id)) return Unauthorized();
        return CompleteLogin(await authService.ChangePasswordAsync(id, request, ct));
    }

    [HttpPost("users/{id}/password-reset")]
    [Authorize(Roles = Roles.Admin)]
    public async Task<IActionResult> ResetPassword(int id, PasswordResetRequest request, CancellationToken ct = default) =>
        this.ToActionResult(await authService.ResetPasswordAsync(id, request, ct));

    [AllowPendingPasswordChange]
    [HttpGet("me")]
    [Authorize]
    [ProducesResponseType<UserResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Me(CancellationToken ct = default)
    {
        if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id)) return Unauthorized();
        return this.ToActionResult(await userService.GetCurrentAsync(id, ct));
    }

    [HttpGet("users")]
    [Authorize(Roles = Roles.Admin)]
    [ProducesResponseType<UserResponse[]>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetUsers([FromQuery] string? search, [FromQuery] string? role, [FromQuery] string? category, [FromQuery] int? branchId, [FromQuery] bool? enabled, CancellationToken ct = default)
    {
        return this.ToActionResult(await userService.GetUsersAsync(search, role, category, branchId, enabled, ct));
    }

    [HttpPatch("users/{id}")]
    [Authorize(Roles = Roles.Admin)]
    public async Task<IActionResult> UpdateUser(int id, UpdateUserRequest request, CancellationToken ct = default) =>
        this.ToActionResult(await userService.UpdateAsync(id, request, ct));

    [HttpPatch("users/{id}/status")]
    [Authorize(Roles = Roles.Admin)]
    [ProducesResponseType<UserMessageResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<MessageResponse>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<MessageResponse>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SetUserStatus(int id, SetUserStatusRequest request, CancellationToken ct = default)
    {
        return this.ToActionResult(await userService.SetUserStatusAsync(id, request, ct));
    }
}
