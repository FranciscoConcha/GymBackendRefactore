using GymBackend.Application.Qr;
using GymBackend.DTOs;
using GymBackend.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GymBackend.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class QrController(IQrService service) : ControllerBase
{
    [HttpGet("my-code")]
    [ProducesResponseType<QrCodeResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<MessageResponse>(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetMyCode(CancellationToken ct)
    {
        var userId = User.GetUserId();
        if (userId is null)
        {
            return Unauthorized(new MessageResponse("La sesión no contiene un usuario válido."));
        }
        return this.ToActionResult(await service.GetMyCodeAsync(userId.Value, ct));
    }
}
