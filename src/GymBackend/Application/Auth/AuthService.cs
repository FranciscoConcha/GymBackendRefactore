using GymBackend.Application.Common;
using GymBackend.Data;
using GymBackend.Domain;
using GymBackend.DTOs;
using Microsoft.EntityFrameworkCore;

namespace GymBackend.Application.Auth;

public sealed class AuthService(AppDbContext context, IUserPasswordHasher passwordHasher, ITokenService tokenService) : IAuthService
{
    public async Task<ServiceResult<AuthResponse>> LoginAsync(LoginRequest request, CancellationToken ct = default)
    {
        var rut = request.Rut.Trim();

        if (!RutFormat.IsValid(rut))
        {
            return ServiceError.Validation("El RUT debe tener entre 8 y 9 dígitos, sin puntos ni guion.");
        }

        if (string.IsNullOrWhiteSpace(request.Password))
        {
            return ServiceError.Validation("La contraseña es obligatoria.");
        }

        var candidates = await context.Users
            .Where(candidate => candidate.Rut == rut)
            .OrderBy(candidate => candidate.Id)
            .ToListAsync(ct);
        var user = candidates.FirstOrDefault(candidate =>
            passwordHasher.Verify(request.Password, candidate.PasswordHash));

        if (user is null || user.Role == Roles.Admin)
        {
            return ServiceError.Unauthorized("Credenciales de usuario incorrectas.");
        }

        return ServiceResult<AuthResponse>.Success(new AuthResponse("Login correcto.", tokenService.CreateToken(user), UserResponse.From(user)));
    }

    public async Task<ServiceResult<AuthResponse>> AdminLoginAsync(AdminLoginRequest request, CancellationToken ct = default)
    {
        var username = request.Username.Trim().ToLowerInvariant();

        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(request.Password))
        {
            return ServiceError.Validation("El usuario y la contraseña son obligatorios.");
        }

        var candidates = await context.Users
            .Where(candidate => candidate.Username.ToLower() == username && candidate.Role == Roles.Admin)
            .OrderBy(candidate => candidate.Id)
            .ToListAsync(ct);
        var admin = candidates.FirstOrDefault(candidate =>
            passwordHasher.Verify(request.Password, candidate.PasswordHash));

        if (admin is null)
        {
            return ServiceError.Unauthorized("Credenciales de administrador incorrectas.");
        }

        return ServiceResult<AuthResponse>.Success(new AuthResponse("Login de administrador correcto.", tokenService.CreateToken(admin), UserResponse.From(admin)));
    }
}
