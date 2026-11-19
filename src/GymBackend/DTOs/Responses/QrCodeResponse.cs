namespace GymBackend.DTOs;

public sealed record QrCodeResponse(string Rut, string Name, string Date, string Token, DateTimeOffset ValidFrom, DateTimeOffset ValidUntil,
    string FirstName, string LastName, string? Category, UserBranchResponse? SportBranch, AccessSessionResponse? NextSession);
