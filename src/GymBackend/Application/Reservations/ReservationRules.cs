namespace GymBackend.Application.Reservations;

public static class ReservationRules
{
    public static bool CanBookOnDate(string? category, int activeBookings) =>
        category != GymBackend.Domain.UserCategories.Estudiante || activeBookings == 0;

    public static bool HasAvailableCapacity(int reservedCount, int capacity) => reservedCount < capacity;
}
