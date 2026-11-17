namespace GymBackend.Options;

public sealed class BootstrapOptions
{
    public const string SectionName = "Bootstrap";

    public string? AdminRut { get; set; }
    public string? AdminEmail { get; set; }
    public string? AdminUsername { get; set; }
    public string? AdminPassword { get; set; }
}
