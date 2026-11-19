using GymBackend.Application.Audit;
using GymBackend.Application.Common;
using GymBackend.Data;
using GymBackend.Domain;
using GymBackend.DTOs;
using GymBackend.Models;
using GymBackend.Time;
using Microsoft.EntityFrameworkCore;
namespace GymBackend.Application.Auth;

public sealed class UserService(AppDbContext context, IUserPasswordHasher passwordHasher, IGymClock clock, IAuditLog audit) : IUserService
{
    public async Task<ServiceResult<UserMessageResponse>> RegisterAsync(RegisterRequest request, CancellationToken ct = default)
    {
        if (request.Role.Equals(Roles.Admin, StringComparison.OrdinalIgnoreCase))
            return ServiceError.Validation("Los administradores deben crearse por un proceso controlado.");
        if (!RutFormat.TryNormalize(request.Rut, out var rut)) return ServiceError.Validation(RutFormat.ValidationMessage);
        var firstName = request.FirstName.Trim();
        var lastName = request.LastName.Trim();
        var email = request.Email.Trim().ToLowerInvariant();
        var username = request.Username?.Trim().ToLowerInvariant() ?? string.Empty;
        if (firstName.Length == 0 || lastName.Length == 0 || email.Length == 0 || string.IsNullOrWhiteSpace(request.Password))
            return ServiceError.Validation("Todos los campos son obligatorios.");
        var invalid = ValidateNamesAndEmail(firstName, lastName, email);
        if (invalid is not null) return ServiceError.Validation(invalid);
        var branch = request.SportBranchId is { } branchId ? await context.SportBranches.FindAsync([branchId], ct) : null;
        invalid = IdentityRules.Validate(request.Role, request.Category, request.SportBranchId, branch?.Enabled == true, username);
        if (invalid is not null) return ServiceError.Validation(invalid);
        if (await context.Users.AnyAsync(u => u.Rut == rut, ct))
            return ServiceError.Validation("El RUT ya est\u00e1 registrado en el sistema.");
        if (await context.Users.AnyAsync(u => u.Email.ToLower() == email, ct))
            return ServiceError.Validation("El correo ya est\u00e1 registrado en el sistema.");
        if (username.Length > 64) return ServiceError.Validation("El usuario admite hasta 64 caracteres.");
        if (username.Length > 0 && await context.Users.AnyAsync(u => u.Username.ToLower() == username, ct))
            return ServiceError.Conflict("El usuario ya est\u00e1 registrado en el sistema.");
        var user = new User
        {
            Rut = rut,
            FirstName = firstName,
            LastName = lastName,
            Email = email,
            Username = request.Role == Roles.Usuario ? string.Empty : username,
            PasswordHash = passwordHasher.Hash(request.Password),
            Role = request.Role,
            Category = request.Category,
            SportBranch = branch,
            CreatedAt = clock.UtcNow,
            UpdatedAt = clock.UtcNow,
        };
        context.Users.Add(user);
        audit.Add("user.created", user);
        return await SaveUserAsync(user, "Usuario registrado correctamente.", ct);
    }

    public async Task<ServiceResult<UserMessageResponse>> UpdateAsync(int id, UpdateUserRequest request, CancellationToken ct = default)
    {
        var user = await context.Users.Include(u => u.SportBranch).SingleOrDefaultAsync(u => u.Id == id, ct);
        if (user is null) return ServiceError.NotFound("Usuario no encontrado.");
        if (user.Role == Roles.Admin || string.Equals(request.Role, Roles.Admin, StringComparison.OrdinalIgnoreCase)) return ServiceError.Forbidden();
        var firstName = request.FirstName?.Trim() ?? user.FirstName;
        var lastName = request.LastName?.Trim() ?? user.LastName;
        var email = request.Email?.Trim().ToLowerInvariant() ?? user.Email;
        var role = request.Role ?? user.Role;
        var category = request.HasCategory ? request.Category : user.Category;
        var branchId = request.HasSportBranchId ? request.SportBranchId : user.SportBranchId;
        var branch = branchId is { } value ? await context.SportBranches.FindAsync([value], ct) : null;
        var invalid = ValidateNamesAndEmail(firstName, lastName, email)
            ?? IdentityRules.Validate(role, category, branchId, branch?.Enabled == true, user.Username);
        if (invalid is not null) return ServiceError.Validation(invalid);
        if (await context.Users.AnyAsync(u => u.Id != id && u.Email.ToLower() == email, ct))
            return ServiceError.Conflict("El correo ya est\u00e1 registrado en el sistema.");
        var fields = new List<string>();
        if (user.FirstName != firstName) fields.Add("firstName");
        if (user.LastName != lastName) fields.Add("lastName");
        if (user.Email != email) fields.Add("email");
        if (user.Role != role) fields.Add("role");
        if (user.Category != category) fields.Add("category");
        if (user.SportBranchId != branchId) fields.Add("sportBranchId");
        user.FirstName = firstName; user.LastName = lastName; user.Email = email;
        user.Role = role; user.Category = category; user.SportBranch = branch; user.SportBranchId = branchId;
        if (fields.Count > 0)
        {
            user.UpdatedAt = clock.UtcNow;
            audit.Add("user.updated", user, fields.ToArray());
        }
        return await SaveUserAsync(user, "Usuario actualizado correctamente.", ct);
    }

    public async Task<ServiceResult<UserResponse[]>> GetUsersAsync(string? search = null, string? role = null,
        string? category = null, int? branchId = null, bool? enabled = null, CancellationToken ct = default)
    {
        var query = context.Users.AsNoTracking().Include(u => u.SportBranch).AsQueryable();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLowerInvariant();
            query = query.Where(u => u.FirstName.ToLower().Contains(term) || u.LastName.ToLower().Contains(term)
                || u.Rut.ToLower().Contains(term) || u.Email.ToLower().Contains(term));
        }
        if (role is not null) query = query.Where(u => u.Role == role);
        if (category is not null) query = query.Where(u => u.Category == category);
        if (branchId is not null) query = query.Where(u => u.SportBranchId == branchId);
        if (enabled is not null) query = query.Where(u => u.Enabled == enabled);
        return ServiceResult<UserResponse[]>.Success((await query.OrderBy(u => u.LastName).ThenBy(u => u.FirstName).ThenBy(u => u.Id).ToListAsync(ct)).Select(UserResponse.From).ToArray());
    }

    public async Task<ServiceResult<UserResponse>> GetCurrentAsync(int id, CancellationToken ct = default)
    {
        var user = await context.Users.AsNoTracking().Include(u => u.SportBranch).SingleOrDefaultAsync(u => u.Id == id, ct);
        return user is null ? ServiceError.Unauthorized("La sesi\u00f3n no corresponde a un usuario v\u00e1lido.")
            : ServiceResult<UserResponse>.Success(UserResponse.From(user));
    }

    public async Task<ServiceResult<UserMessageResponse>> SetUserStatusAsync(int id, SetUserStatusRequest request, CancellationToken ct = default)
    {
        var user = await context.Users.Include(u => u.SportBranch).SingleOrDefaultAsync(u => u.Id == id, ct);
        if (user is null) return ServiceError.NotFound("Usuario no encontrado.");
        if (user.Role == Roles.Admin) return ServiceError.Validation("No se puede deshabilitar a un administrador.");
        if (user.Enabled != request.Enabled)
        {
            user.Enabled = request.Enabled;
            user.UpdatedAt = clock.UtcNow;
            audit.Add("user.status_changed", user, "enabled");
        }
        return await SaveUserAsync(user, user.Enabled ? "Usuario habilitado correctamente." : "Usuario deshabilitado correctamente.", ct);
    }

    private static string? ValidateNamesAndEmail(string firstName, string lastName, string email)
    {
        if (firstName.Length > 80 || !InputRules.IsSafeName(firstName)) return "El nombre contiene caracteres no permitidos o supera los 80 caracteres.";
        if (lastName.Length > 80 || !InputRules.IsSafeName(lastName)) return "El apellido contiene caracteres no permitidos o supera los 80 caracteres.";
        return InputRules.IsValidEmail(email) ? null : "El correo no tiene un formato v\u00e1lido.";
    }

    private async Task<ServiceResult<UserMessageResponse>> SaveUserAsync(User user, string message, CancellationToken ct)
    {
        try { await context.SaveChangesAsync(ct); }
        catch (DbUpdateException exception) when (exception.IsUniqueViolation())
        { return ServiceError.Conflict("El RUT, el correo o el usuario ya est\u00e1 registrado en el sistema."); }
        return ServiceResult<UserMessageResponse>.Success(new(message, UserResponse.From(user)));
    }
}
