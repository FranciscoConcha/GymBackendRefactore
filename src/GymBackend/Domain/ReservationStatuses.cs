namespace GymBackend.Domain;

public static class ReservationStatuses
{
    public static string Name(ReservationStatus status) => status switch
    {
        ReservationStatus.Reserved => Reserved,
        ReservationStatus.Cancelled => Cancelled,
        ReservationStatus.Attended => Attended,
        _ => throw new ArgumentOutOfRangeException(nameof(status)),
    };

    public static ReservationStatus Parse(string value) => value switch
    {
        Reserved => ReservationStatus.Reserved,
        Cancelled => ReservationStatus.Cancelled,
        Attended => ReservationStatus.Attended,
        _ => throw new ArgumentException("Estado de reserva inválido.", nameof(value)),
    };

    public const string Reserved = "Reservado";
    public const string Cancelled = "Cancelado";
    public const string Attended = "Asistió";
}

