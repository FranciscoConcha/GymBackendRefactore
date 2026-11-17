using GymBackend.DTOs;
using GymBackend.Data;
using GymBackend.Time;
using GymBackend.Options;
using Microsoft.Extensions.Options;
using GymBackend.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using GymBackend.Extensions;

namespace GymBackend.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class QrController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly IGymClock _clock;
    private readonly IOptions<JwtOptions> _jwtOptions;

    public QrController(AppDbContext context, IOptions<JwtOptions> jwtOptions, IGymClock clock)
    {
        _context = context;
        _clock = clock;
        _jwtOptions = jwtOptions;
    }

    [HttpGet("my-code")]
    [ProducesResponseType<QrCodeResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<MessageResponse>(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetMyCode()
    {
        var userId = User.GetUserId();
        if (userId is null)
        {
            return Unauthorized(new MessageResponse("La sesión no contiene un usuario válido."));
        }
        var user = await _context.Users.FindAsync(userId.Value);

        if (user is null)
        {
            return Unauthorized(new MessageResponse("La sesión no corresponde a un usuario válido."));
        }

        var signingKey = _jwtOptions.Value.Key
            ?? throw new InvalidOperationException("Jwt:Key no está configurada.");

        var today = _clock.Today;
        var token = QrTokenService.GenerateToken(user.Rut, signingKey, today.ToDateTime(TimeOnly.MinValue));

        return Ok(new QrCodeResponse(user.Rut, user.Name, today.ToString("yyyy-MM-dd"), token,
            _clock.StartOfDay(today), _clock.StartOfDay(today.AddDays(1)).AddMinutes(-1)));
    }
}
