using GymBackend.Application.Audit;
using GymBackend.Application.Common;
using GymBackend.Data;
using GymBackend.Domain;
using GymBackend.DTOs;
using GymBackend.Time;
using Microsoft.EntityFrameworkCore;

namespace GymBackend.Application.Auth;

public sealed class AuthService(AppDbContext context, IUserPasswordHasher passwordHasher, ITokenService tokenService, IGymClock clock, IAuditLog audit) : IAuthService
{
    public async Task<ServiceResult<AuthSession>> LoginAsync(LoginRequest request, CancellationToken ct = default)
    {
        if (!RutFormat.TryNormalize(request.Rut, out var rut))
        {
            return ServiceError.Validation(RutFormat.ValidationMessage);
        }

        if (string.IsNullOrWhiteSpace(request.Password))
        {
            return ServiceError.Validation("La contraseña es obligatoria.");
        }

        var candidates = await context.Users.Include(candidate => candidate.SportBranch)
            .Where(candidate => candidate.Rut == rut)
            .OrderBy(candidate => candidate.Id)
            .ToListAsync(ct);
        if (!candidates.Any(candidate => candidate.AccountStatus == AccountStatus.Activa)) passwordHasher.VerifyDummy(request.Password);
        var match = candidates.Where(candidate => candidate.AccountStatus == AccountStatus.Activa).Select(candidate => (User: candidate, Check: passwordHasher.Verify(request.Password, candidate.PasswordHash)))
            .FirstOrDefault(candidate => candidate.Check != PasswordCheck.Failed);
        var user = match.User;

        if (user is null || !user.Enabled || user.Role != Roles.Usuario)
        {
            return ServiceError.Unauthorized("Credenciales de usuario incorrectas.");
        }

        await RehashIfNeededAsync(user, request.Password, match.Check, ct);

        return ServiceResult<AuthSession>.Success(new AuthSession("Login correcto.", tokenService.CreateToken(user), UserResponse.From(user)));
    }

    public async Task<ServiceResult<AuthSession>> AdminLoginAsync(AdminLoginRequest request, CancellationToken ct = default)
    {
        var username = request.Username.Trim().ToLowerInvariant();

        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(request.Password))
        {
            return ServiceError.Validation("El usuario y la contraseña son obligatorios.");
        }

        var candidates = await context.Users.Include(candidate => candidate.SportBranch)
            .Where(candidate => candidate.Username.ToLower() == username && (candidate.Role == Roles.Admin || candidate.Role == Roles.Recepcionista))
            .OrderBy(candidate => candidate.Id)
            .ToListAsync(ct);
        if (!candidates.Any(candidate => candidate.AccountStatus == AccountStatus.Activa)) passwordHasher.VerifyDummy(request.Password);
        var match = candidates.Where(candidate => candidate.AccountStatus == AccountStatus.Activa).Select(candidate => (User: candidate, Check: passwordHasher.Verify(request.Password, candidate.PasswordHash)))
            .FirstOrDefault(candidate => candidate.Check != PasswordCheck.Failed);
        var admin = match.User;

        if (admin is null || !admin.Enabled)
        {
            return ServiceError.Unauthorized("Credenciales de administrador incorrectas.");
        }

        await RehashIfNeededAsync(admin, request.Password, match.Check, ct);

        return ServiceResult<AuthSession>.Success(new AuthSession("Login de administrador correcto.", tokenService.CreateToken(admin), UserResponse.From(admin)));
    }

    public async Task<ServiceResult<AuthSession>> ChangePasswordAsync(int id, ChangePasswordRequest request, CancellationToken ct = default)
    {
        await using var transaction = await context.Database.BeginTransactionAsync(ct);
        await LockUserAsync(id, ct);
        var user = await context.Users.Include(u => u.SportBranch).SingleOrDefaultAsync(u => u.Id == id, ct);
        if (user is null || !user.Enabled) return ServiceError.Unauthorized("La sesi\u00f3n no es v\u00e1lida.");
        if (string.IsNullOrEmpty(request.CurrentPassword)
            || passwordHasher.Verify(request.CurrentPassword, user.PasswordHash) == PasswordCheck.Failed)
            return ServiceError.Validation("La contrase\u00f1a actual no es correcta.");
        var invalid = ValidateNewPassword(request.NewPassword);
        if (invalid is not null) return ServiceError.Validation(invalid);
        if (passwordHasher.Verify(request.NewPassword, user.PasswordHash) != PasswordCheck.Failed)
            return ServiceError.Validation("La nueva contrase\u00f1a debe ser distinta de la actual.");
        user.PasswordHash = passwordHasher.Hash(request.NewPassword);
        user.MustChangePassword = false;
        user.SessionVersion++;
        user.UpdatedAt = clock.UtcNow;
        audit.Add("user.password_changed", user);
        await context.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return ServiceResult<AuthSession>.Success(new("Contrase\u00f1a actualizada correctamente.", tokenService.CreateToken(user), UserResponse.From(user)));
    }

    public async Task<ServiceResult<UserMessageResponse>> ResetPasswordAsync(int id, PasswordResetRequest request, CancellationToken ct = default)
    {
        await using var transaction = await context.Database.BeginTransactionAsync(ct);
        await LockUserAsync(id, ct);
        var user = await context.Users.Include(u => u.SportBranch).SingleOrDefaultAsync(u => u.Id == id, ct);
        if (user is null) return ServiceError.NotFound("Usuario no encontrado.");
        if (user.Role == Roles.Admin) return ServiceError.Forbidden();
        var invalid = ValidateNewPassword(request.TemporaryPassword);
        if (invalid is not null) return ServiceError.Validation(invalid);
        user.PasswordHash = passwordHasher.Hash(request.TemporaryPassword);
        user.AccountStatus = AccountStatus.Activa;
        user.MustChangePassword = true;
        user.SessionVersion++;
        user.UpdatedAt = clock.UtcNow;
        audit.Add("user.password_reset", user);
        await context.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return ServiceResult<UserMessageResponse>.Success(new("Contrase\u00f1a temporal asignada correctamente.", UserResponse.From(user)));
    }

    public async Task LogoutAsync(int id, CancellationToken ct = default)
    {
        await context.Users.Where(u => u.Id == id).ExecuteUpdateAsync(setters => setters
            .SetProperty(u => u.SessionVersion, u => u.SessionVersion + 1)
            .SetProperty(u => u.UpdatedAt, clock.UtcNow), ct);
    }

    private Task LockUserAsync(int id, CancellationToken ct) => context.Database.ExecuteSqlInterpolatedAsync(
        $"SELECT 1 FROM \"Users\" WHERE \"Id\" = {id} FOR UPDATE", ct);

    private static string? ValidateNewPassword(string? password) => password is null || password.Length is < 8 or > 128
        ? "La nueva contrase\u00f1a debe tener entre 8 y 128 caracteres." : null;

    private async Task RehashIfNeededAsync(GymBackend.Models.User user, string password, PasswordCheck check, CancellationToken ct)
    {
        if (check != PasswordCheck.SuccessRehashNeeded) return;
        user.PasswordHash = passwordHasher.Hash(password);
        user.UpdatedAt = clock.UtcNow;
        await context.SaveChangesAsync(ct);
    }
}
