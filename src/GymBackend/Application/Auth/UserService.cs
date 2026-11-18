using GymBackend.Application.Common;
using GymBackend.Data;
using GymBackend.Domain;
using GymBackend.DTOs;
using GymBackend.Models;
using Microsoft.EntityFrameworkCore;

namespace GymBackend.Application.Auth;

public sealed class UserService(AppDbContext context, IUserPasswordHasher passwordHasher) : IUserService
{
    public async Task<ServiceResult<UserMessageResponse>> RegisterAsync(RegisterRequest request, CancellationToken ct = default)
    {
        var name = request.Name.Trim();
        var email = request.Email.Trim().ToLowerInvariant();
        var userType = request.UserType.Trim();

        if (!RutFormat.TryNormalize(request.Rut, out var rut))
        {
            return ServiceError.Validation(RutFormat.ValidationMessage);
        }

        if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(request.Password))
        {
            return ServiceError.Validation("Todos los campos son obligatorios.");
        }

        if (userType.Equals(UserTypes.Administrator, StringComparison.OrdinalIgnoreCase))
        {
            return ServiceError.Validation("Los administradores deben crearse por un proceso controlado.");
        }

        if (!InputRules.IsSafeName(name))
        {
            return ServiceError.Validation("El nombre contiene caracteres no permitidos.");
        }

        if (!InputRules.IsValidEmail(email))
        {
            return ServiceError.Validation("El correo no tiene un formato válido.");
        }

        if (await context.Users.AnyAsync(user => user.Rut == rut, ct))
        {
            return ServiceError.Validation("El RUT ya está registrado en el sistema.");
        }

        if (await context.Users.AnyAsync(user => user.Email.ToLower() == email, ct))
        {
            return ServiceError.Validation("El correo ya está registrado en el sistema.");
        }

        var user = new User
        {
            Rut = rut,
            Name = name,
            Email = email,
            Username = string.Empty,
            PasswordHash = passwordHasher.Hash(request.Password),
            UserType = userType,
            Role = Roles.Client,
        };

        context.Users.Add(user);
        try
        {
            await context.SaveChangesAsync(ct);
        }
        catch (DbUpdateException exception) when (exception.IsUniqueViolation())
        {
            return ServiceError.Conflict("El RUT, el correo o el usuario ya está registrado en el sistema.");
        }

        return ServiceResult<UserMessageResponse>.Success(new UserMessageResponse("Usuario registrado correctamente.", UserResponse.From(user)));
    }

    public async Task<ServiceResult<UserResponse[]>> GetUsersAsync(CancellationToken ct = default)
    {
        var users = (await context.Users
            .OrderBy(user => user.Name)
            .ToListAsync(ct))
            .Select(UserResponse.From).ToArray();

        return ServiceResult<UserResponse[]>.Success(users);
    }

    public async Task<ServiceResult<UserResponse>> GetCurrentAsync(int id, CancellationToken ct = default)
    {
        var user = await context.Users.AsNoTracking().SingleOrDefaultAsync(user => user.Id == id, ct);
        return user is null
            ? ServiceError.Unauthorized("La sesión no corresponde a un usuario válido.")
            : ServiceResult<UserResponse>.Success(UserResponse.From(user));
    }

    public async Task<ServiceResult<UserMessageResponse>> SetUserStatusAsync(int id, SetUserStatusRequest request, CancellationToken ct = default)
    {
        var user = await context.Users.FindAsync(new object[] { id }, ct);

        if (user is null)
        {
            return ServiceError.NotFound("Usuario no encontrado.");
        }

        if (user.Role == Roles.Admin)
        {
            return ServiceError.Validation("No se puede deshabilitar a un administrador.");
        }

        user.Enabled = request.Enabled;
        await context.SaveChangesAsync(ct);

        return ServiceResult<UserMessageResponse>.Success(new UserMessageResponse(user.Enabled ? "Usuario habilitado correctamente." : "Usuario deshabilitado correctamente.", UserResponse.From(user)));
    }
}
