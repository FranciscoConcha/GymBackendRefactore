using System.Globalization;
using GymBackend.Models;

namespace GymBackend.DTOs;

public sealed record AccessUserResponse(int Id, string FirstName, string LastName, string FullName, string Rut,
    string? Category, UserBranchResponse? SportBranch)
{
    public static AccessUserResponse From(User user) => new(user.Id, user.FirstName, user.LastName, user.FullName,
        user.Rut, user.Category, user.SportBranch is { } b ? new(b.Id, b.Name) : null);
}

public sealed record AccessSessionResponse(string Kind, int? ReservationId, int? GroupBookingId, string Date, string StartTime, string EndTime)
{
    public static AccessSessionResponse From(Reservation reservation) => Create("Reserva", reservation.Id, null, reservation.Date, reservation.ScheduleSlot);
    public static AccessSessionResponse From(GroupBooking group) => Create("Grupo", null, group.Id, group.Date, group.ScheduleSlot);
    private static AccessSessionResponse Create(string kind, int? reservation, int? group, DateOnly date, ScheduleSlot slot) =>
        new(kind, reservation, group, date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            slot.StartTime.ToString("HH:mm", CultureInfo.InvariantCulture), slot.EndTime.ToString("HH:mm", CultureInfo.InvariantCulture));
}

public sealed record OccupancyResponse(int Current, int Max);
public sealed record AccessValidationResponse(AccessUserResponse User, AccessSessionResponse? Session,
    bool Inside, bool CanEnter, string? Reason, OccupancyResponse Occupancy, string Action = "Entrada", bool CanExit = false);
public sealed record CurrentPersonResponse(int UserId, string FullName, string Rut, string? Category, DateTime EntryAt, string SessionEndTime);
public sealed record CurrentAccessResponse(OccupancyResponse Occupancy, CurrentPersonResponse[] People);
public sealed record VisitMessageResponse(string Message, VisitResponse Visit);
public sealed record VisitResponse(int Id, int UserId, string FullName, string Rut, int? ReservationId, int? GroupBookingId,
    string Date, DateTime EntryAt, DateTime? ExitAt, double? StayMinutes, string DocumentType, string EntryOperator, string? ExitOperator)
{
    public static VisitResponse From(Visit visit) => new(visit.Id, visit.UserId, visit.User.FullName, visit.User.Rut,
        visit.ReservationId, visit.GroupBookingId, visit.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        visit.EntryAt, visit.ExitAt, visit.ExitAt is { } exit ? (exit - visit.EntryAt).TotalMinutes : null,
        visit.DocumentType, visit.EntryOperator.FullName, visit.ExitOperator?.FullName);
}
