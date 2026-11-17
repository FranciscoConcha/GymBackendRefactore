using GymBackend.Application.Common;
using GymBackend.DTOs;

namespace GymBackend.Application.Auth;

public interface IAuthService
{
    Task<ServiceResult<AuthResponse>> LoginAsync(LoginRequest request, CancellationToken ct = default);
    Task<ServiceResult<AuthResponse>> AdminLoginAsync(AdminLoginRequest request, CancellationToken ct = default);
}
