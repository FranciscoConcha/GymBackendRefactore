namespace GymBackend.Options;

public sealed class GymOptions
{
    public const string SectionName = "Gym";
    public int MaxOccupancy { get; set; } = 30;

    public string TimeZone { get; set; } = "America/Santiago";
}
