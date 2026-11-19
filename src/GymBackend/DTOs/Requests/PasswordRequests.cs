namespace GymBackend.DTOs;

public sealed record ChangePasswordRequest(string CurrentPassword, string NewPassword);
public sealed record PasswordResetRequest(string TemporaryPassword);
