namespace GymBackend.Options;

public enum EmailMode { Disabled, Pickup, Smtp }

public sealed class EmailOptions
{
    public const string SectionName = "Email";
    public EmailMode Mode { get; set; } = EmailMode.Disabled;
    public string Host { get; set; } = string.Empty;
    public int Port { get; set; } = 587;
    public bool EnableSsl { get; set; } = true;
    public string User { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string From { get; set; } = "no-reply@example.test";
    public string PickupDirectory { get; set; } = string.Empty;
}
