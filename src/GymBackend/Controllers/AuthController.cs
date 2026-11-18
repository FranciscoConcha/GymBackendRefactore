using GymBackend.Application.Auth;
using GymBackend.Domain;
using GymBackend.DTOs;
using GymBackend.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace GymBackend.Controllers;

[ApiController]
[Route("api/[controller]")]
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
    [ProducesResponseType<MessageResponse>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<MessageResponse>(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Login(LoginRequest request, CancellationToken ct = default)
    {
        return this.ToActionResult(await authService.LoginAsync(request, ct));
    }

    [HttpPost("admin-login")]
    [EnableRateLimiting(RateLimitingExtensions.LoginPolicy)]
    [ProducesResponseType<AuthResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<MessageResponse>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<MessageResponse>(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> AdminLogin(AdminLoginRequest request, CancellationToken ct = default)
    {
        return this.ToActionResult(await authService.AdminLoginAsync(request, ct));
    }

    [HttpGet("users")]
    [Authorize(Roles = Roles.Admin)]
    [ProducesResponseType<UserResponse[]>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetUsers(CancellationToken ct = default)
    {
        return this.ToActionResult(await userService.GetUsersAsync(ct));
    }

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
