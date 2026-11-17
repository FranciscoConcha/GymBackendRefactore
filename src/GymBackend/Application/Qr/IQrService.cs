using GymBackend.Application.Common;
using GymBackend.DTOs;

namespace GymBackend.Application.Qr;

public interface IQrService
{
    Task<ServiceResult<QrCodeResponse>> GetMyCodeAsync(int userId, CancellationToken ct = default);
}
