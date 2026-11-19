using GymBackend.Application.Common;
using GymBackend.Data;
using GymBackend.DTOs;
using GymBackend.Time;

namespace GymBackend.Application.Qr;

public sealed class QrService(AppDbContext context, IQrTokenService tokens, IGymClock clock) : IQrService
{
    private readonly AppDbContext _context = context;
    private readonly IGymClock _clock = clock;

    public async Task<ServiceResult<QrCodeResponse>> GetMyCodeAsync(int userId, CancellationToken ct = default)
    {
        var user = await _context.Users.FindAsync(new object[] { userId }, ct);

        if (user is null)
        {
            return ServiceError.Unauthorized("La sesión no corresponde a un usuario válido.");
        }

        var today = _clock.Today;
        var token = tokens.Generate(user.Rut, today);

        return ServiceResult<QrCodeResponse>.Success(new QrCodeResponse(user.Rut, user.FullName, today.ToString("yyyy-MM-dd"), token,
            _clock.StartOfDay(today), _clock.StartOfDay(today.AddDays(1)).AddMinutes(-1)));
    }
}
