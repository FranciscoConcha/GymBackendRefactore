using GymBackend.Application.Common;
using GymBackend.DTOs;

namespace GymBackend.Application.Auth;

public interface IAuthService
{
    Task<ServiceResult<AuthSession>> ChangePasswordAsync(int id, ChangePasswordRequest request, CancellationToken ct = default);
    Task<ServiceResult<UserMessageResponse>> ResetPasswordAsync(int id, PasswordResetRequest request, CancellationToken ct = default);
    Task LogoutAsync(int id, CancellationToken ct = default);
    Task<ServiceResult<AuthSession>> LoginAsync(LoginRequest request, CancellationToken ct = default);
    Task<ServiceResult<AuthSession>> AdminLoginAsync(AdminLoginRequest request, CancellationToken ct = default);
}
