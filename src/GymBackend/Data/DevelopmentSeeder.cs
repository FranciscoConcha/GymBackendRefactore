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
        if (!await db.ScheduleSlots.AnyAsync(ct))
        {
            var days = new[] { DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday, DayOfWeek.Saturday };
            var ranges = new[] { (Start: "08:00", End: "09:30"), (Start: "09:30", End: "11:00"), (Start: "11:00", End: "12:30"), (Start: "13:00", End: "14:30") };
            db.ScheduleSlots.AddRange(days.SelectMany(day => ranges.Select(range => new ScheduleSlot
            {
                Day = day,
                StartTime = TimeOnly.ParseExact(range.Start, "HH:mm", System.Globalization.CultureInfo.InvariantCulture),
                EndTime = TimeOnly.ParseExact(range.End, "HH:mm", System.Globalization.CultureInfo.InvariantCulture),
                Zone = Zones.Default,
                Capacity = 20,
                Enabled = true,
            })));
            await db.SaveChangesAsync(ct);
        }
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
                Name = UserTypes.Administrator,
                Email = email.Trim().ToLowerInvariant(),
                Username = username.Trim().ToLowerInvariant(),
                PasswordHash = passwordHasher.Hash(adminPassword),
                UserType = UserTypes.Administrator,
                Role = Roles.Admin,
                Enabled = true,
            });
            await db.SaveChangesAsync(ct);
        }
    }
}
