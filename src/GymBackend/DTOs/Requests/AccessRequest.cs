namespace GymBackend.DTOs;

// Business validation is performed by AccessService so rejected attempts are also recorded.
public sealed class AccessRequest
{
    public string? Token { get; set; }
    public string? DocumentType { get; set; }
}
