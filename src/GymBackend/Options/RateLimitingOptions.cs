namespace GymBackend.Options;

public sealed class RateLimitingOptions
{
    public const string SectionName = "RateLimiting";

    public LoginRateLimitingOptions Login { get; set; } = new();
}

public sealed class LoginRateLimitingOptions
{
    public int PermitLimit { get; set; } = 5;
    public int WindowSeconds { get; set; } = 60;
}
