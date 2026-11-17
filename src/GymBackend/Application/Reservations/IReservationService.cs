using GymBackend.Application.Common;
using GymBackend.DTOs;

namespace GymBackend.Application.Reservations;

public interface IReservationService
{
    Task<ServiceResult<ReservationResponse[]>> GetAllAsync(CancellationToken ct = default);
    Task<ServiceResult<ReservationResponse[]>> GetByUserAsync(int requestedUserId, int? currentUserId, bool isAdmin, CancellationToken ct = default);
    Task<ServiceResult<ReservationMessageResponse>> CreateAsync(int userId, CreateReservationRequest request, CancellationToken ct = default);
    Task<ServiceResult<ReservationMessageResponse>> CancelAsync(int id, int? currentUserId, bool isAdmin, CancellationToken ct = default);
}
