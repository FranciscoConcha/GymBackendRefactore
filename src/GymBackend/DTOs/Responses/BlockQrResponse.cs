namespace GymBackend.DTOs;

public sealed record QrUserResponse(string FirstName, string LastName, string FullName, string Rut, string? Category, UserBranchResponse? SportBranch);
public sealed record BlockQrResponse(string Token, DateTimeOffset ValidFrom, DateTimeOffset ValidUntil, QrUserResponse User, AccessSessionResponse Session);
