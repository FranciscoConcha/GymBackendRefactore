using GymBackend.Application.Common;
using GymBackend.DTOs;

namespace GymBackend.Application.Auth;

public interface IUserService
{
    Task<ServiceResult<UserMessageResponse>> RegisterAsync(RegisterRequest request, CancellationToken ct = default);
    Task<ServiceResult<UserMessageResponse>> UpdateAsync(int id, UpdateUserRequest request, CancellationToken ct = default);
    Task<ServiceResult<UserResponse[]>> GetUsersAsync(string? search = null, string? role = null, string? category = null, int? branchId = null, bool? enabled = null, CancellationToken ct = default);
    Task<ServiceResult<UserResponse>> GetCurrentAsync(int id, CancellationToken ct = default);
    Task<ServiceResult<UserMessageResponse>> SetUserStatusAsync(int id, SetUserStatusRequest request, CancellationToken ct = default);
}
