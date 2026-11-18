using GymBackend.Application.Common;
using GymBackend.DTOs;

namespace GymBackend.Application.Auth;

public interface IAuthService
{
    Task<ServiceResult<AuthSession>> LoginAsync(LoginRequest request, CancellationToken ct = default);
    Task<ServiceResult<AuthSession>> AdminLoginAsync(AdminLoginRequest request, CancellationToken ct = default);
}
