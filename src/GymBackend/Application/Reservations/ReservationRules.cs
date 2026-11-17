namespace GymBackend.Application.Reservations;

public static class ReservationRules
{
    public static bool HasAvailableCapacity(int reservedCount, int capacity) => reservedCount < capacity;
}
