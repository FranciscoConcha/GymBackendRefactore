namespace GymBackend.Options;

public sealed class AccountsOptions
{
    public const string SectionName = "Accounts";
    public string FrontendBaseUrl { get; set; } = string.Empty;
    public int ActivationHours { get; set; } = 72;
    public int ResetMinutes { get; set; } = 60;
}
