using GymBackend.Models;

namespace GymBackend.DTOs;

public sealed record ReservationResponse(int Id, int UserId, string UserName, string Day, string StartTime, string EndTime, string Zone, int Capacity, string Status, DateTime CreatedAt)
{
    public static ReservationResponse From(Reservation entity) =>
        new(entity.Id, entity.UserId, entity.UserName, entity.Day, entity.StartTime, entity.EndTime, entity.Zone, entity.Capacity, entity.Status, entity.CreatedAt);
}
