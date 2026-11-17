using GymBackend.Application.Common;
using GymBackend.DTOs;

namespace GymBackend.Application.Auth;

public interface IUserService
{
    Task<ServiceResult<UserMessageResponse>> RegisterAsync(RegisterRequest request, CancellationToken ct = default);
    Task<ServiceResult<UserResponse[]>> GetUsersAsync(CancellationToken ct = default);
    Task<ServiceResult<UserMessageResponse>> SetUserStatusAsync(int id, SetUserStatusRequest request, CancellationToken ct = default);
}
