using GymBackend.Application.Common;
using GymBackend.DTOs;

namespace GymBackend.Application.Attendance;

public interface IAttendanceService
{
    Task<ServiceResult<AttendanceMessageResponse>> RegisterEntryAsync(string token, string adminRut, CancellationToken ct = default);
    Task<ServiceResult<AttendanceMessageResponse>> RegisterExitAsync(string token, string adminRut, CancellationToken ct = default);
}
