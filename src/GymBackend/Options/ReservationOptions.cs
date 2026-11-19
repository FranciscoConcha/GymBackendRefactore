namespace GymBackend.Options;

public enum ReservationHorizonUnit { CalendarDays, BusinessDays }

public sealed class ReservationOptions
{
    public const string SectionName = "Reservations";
    public Dictionary<string, int> CategoryMaxDaysAhead { get; set; } = [];
    public int MaxDaysAhead { get; set; } = 7;
    public ReservationHorizonUnit HorizonUnit { get; set; } = ReservationHorizonUnit.CalendarDays;
}
