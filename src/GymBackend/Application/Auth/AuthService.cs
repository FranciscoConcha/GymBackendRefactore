using GymBackend.Application.Common;
using GymBackend.Data;
using GymBackend.Domain;
using GymBackend.DTOs;
using GymBackend.Time;
using Microsoft.EntityFrameworkCore;

namespace GymBackend.Application.Auth;

public sealed class AuthService(AppDbContext context, IUserPasswordHasher passwordHasher, ITokenService tokenService, IGymClock clock) : IAuthService
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
        if (candidates.Count == 0) passwordHasher.VerifyDummy(request.Password);
        var match = candidates.Select(candidate => (User: candidate, Check: passwordHasher.Verify(request.Password, candidate.PasswordHash)))
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
        if (candidates.Count == 0) passwordHasher.VerifyDummy(request.Password);
        var match = candidates.Select(candidate => (User: candidate, Check: passwordHasher.Verify(request.Password, candidate.PasswordHash)))
            .FirstOrDefault(candidate => candidate.Check != PasswordCheck.Failed);
        var admin = match.User;

        if (admin is null || !admin.Enabled)
        {
            return ServiceError.Unauthorized("Credenciales de administrador incorrectas.");
        }

        await RehashIfNeededAsync(admin, request.Password, match.Check, ct);

        return ServiceResult<AuthSession>.Success(new AuthSession("Login de administrador correcto.", tokenService.CreateToken(admin), UserResponse.From(admin)));
    }

    private async Task RehashIfNeededAsync(GymBackend.Models.User user, string password, PasswordCheck check, CancellationToken ct)
    {
        if (check != PasswordCheck.SuccessRehashNeeded) return;
        user.PasswordHash = passwordHasher.Hash(password);
        user.UpdatedAt = clock.UtcNow;
        await context.SaveChangesAsync(ct);
    }
}
