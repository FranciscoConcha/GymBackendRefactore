using GymBackend.Application.Auth;
using GymBackend.Domain;
using GymBackend.Models;
using GymBackend.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace GymBackend.Data;

public static class DevelopmentSeeder
{
    public static async Task SeedAsync(IServiceProvider services, IHostEnvironment env, CancellationToken ct = default)
    {
        if (!env.IsDevelopment())
            throw new InvalidOperationException("Database:Initialize solo se permite en Development.");
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.MigrateAsync(ct);
        var clock = scope.ServiceProvider.GetRequiredService<GymBackend.Time.IGymClock>();
        var bootstrap = scope.ServiceProvider.GetRequiredService<IOptions<BootstrapOptions>>().Value;
        var adminPassword = bootstrap.AdminPassword;
        if (!await db.Users.AnyAsync(user => user.Role == Roles.Admin, ct) && !string.IsNullOrWhiteSpace(adminPassword))
        {
            var validRut = RutFormat.TryNormalize(bootstrap.AdminRut, out var rut);
            var email = bootstrap.AdminEmail;
            var username = bootstrap.AdminUsername;
            if (adminPassword.Length < 16 || !validRut || string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(username))
                throw new InvalidOperationException("Bootstrap requiere RUT, correo, usuario y una contrase\u00f1a de al menos 16 caracteres.");
            var passwordHasher = scope.ServiceProvider.GetRequiredService<IUserPasswordHasher>();
            db.Users.Add(new User
            {
                Rut = rut,
                FirstName = "Administrador",
                LastName = "Sistema",
                CreatedAt = clock.UtcNow,
                UpdatedAt = clock.UtcNow,
                Email = email.Trim().ToLowerInvariant(),
                Username = username.Trim().ToLowerInvariant(),
                PasswordHash = passwordHasher.Hash(adminPassword),
                Role = Roles.Admin,
                MustChangePassword = false,
                Enabled = true,
            });
            await db.SaveChangesAsync(ct);
        }
    }
}
