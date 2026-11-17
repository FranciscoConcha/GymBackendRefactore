using GymBackend.DTOs;
using GymBackend.Domain;
using GymBackend.Application.Reservations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using GymBackend.Extensions;

namespace GymBackend.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ReservationsController : ControllerBase
{
    private readonly IReservationService _service;

    public ReservationsController(IReservationService service)
    {
        _service = service;
    }

    [HttpGet]
    [Authorize(Roles = Roles.Admin)]
    [ProducesResponseType<ReservationResponse[]>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetReservations(CancellationToken ct = default)
    {
        return this.ToActionResult(await _service.GetAllAsync(ct));
    }

    [HttpGet("user/{userId}")]
    [ProducesResponseType<ReservationResponse[]>(StatusCodes.Status200OK)]
    [ProducesResponseType<MessageResponse>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetReservationsByUser(int userId, CancellationToken ct = default)
    {
        return this.ToActionResult(await _service.GetByUserAsync(userId, User.GetUserId(), User.IsInRole(Roles.Admin), ct));
    }

    [HttpPost]
    [ProducesResponseType<ReservationMessageResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<MessageResponse>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<MessageResponse>(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> CreateReservation(CreateReservationRequest request, CancellationToken ct = default)
    {
        var userId = User.GetUserId();
        if (userId is null)
        {
            return Unauthorized(new MessageResponse("La sesión no contiene un usuario válido."));
        }

        return this.ToActionResult(await _service.CreateAsync(userId.Value, request, ct));
    }

    [HttpDelete("{id}")]
    [ProducesResponseType<ReservationMessageResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<MessageResponse>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<MessageResponse>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CancelReservation(int id, CancellationToken ct = default)
    {
        return this.ToActionResult(await _service.CancelAsync(id, User.GetUserId(), User.IsInRole(Roles.Admin), ct));
    }
}
