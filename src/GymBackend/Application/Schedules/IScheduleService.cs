using GymBackend.Application.Common;
using GymBackend.DTOs;

namespace GymBackend.Application.Schedules;

public interface IScheduleService
{
    Task<ServiceResult<ScheduleResponse[]>> GetEnabledAsync(CancellationToken ct = default);
}
