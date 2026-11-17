namespace GymBackend.Domain;

public static class GymWeekdays
{
    private static readonly string[] Names =
    {
        "Domingo", "Lunes", "Martes", "Miércoles", "Jueves", "Viernes", "Sábado",
    };

    public static string Name(DayOfWeek day) => Names[(int)day];

    public static IReadOnlyList<string> OpeningDays { get; } = Array.AsReadOnly(Names[1..]);
}
