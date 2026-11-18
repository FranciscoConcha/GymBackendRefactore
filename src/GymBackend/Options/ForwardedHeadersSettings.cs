namespace GymBackend.Options;

public sealed class ForwardedHeadersSettings
{
    public const string SectionName = "ForwardedHeaders";

    public bool Enabled { get; set; }
    public string[] KnownProxies { get; set; } = [];
    public string[] KnownNetworks { get; set; } = [];
}
