namespace GymBackend.Options;

public sealed class QrOptions
{
    public const string SectionName = "Qr";

    public string SigningKey { get; set; } = string.Empty;
}
