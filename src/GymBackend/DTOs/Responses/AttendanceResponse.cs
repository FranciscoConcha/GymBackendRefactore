using GymBackend.Models;

namespace GymBackend.DTOs;

public sealed record AttendanceResponse(int Id, int UserId, string Rut, string UserName, string Date, DateTime? EntryTime, string? EntryAdminRut, DateTime? ExitTime, string? ExitAdminRut, int? ReservationId, bool CheckedIn)
{
    public static AttendanceResponse From(Attendance entity) =>
        new(entity.Id, entity.UserId, entity.Rut, entity.UserName, entity.Date, entity.EntryTime, entity.EntryAdminRut, entity.ExitTime, entity.ExitAdminRut, entity.ReservationId, entity.CheckedIn);
}
