namespace GymBackend.DTOs;

public sealed class AccountPasswordRequest
{
    public string? Token { get; set; }
    public string? NewPassword { get; set; }
}

public sealed class RequestPasswordResetRequest
{
    public string? Email { get; set; }
}
