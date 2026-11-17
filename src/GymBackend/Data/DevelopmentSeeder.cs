using GymBackend.Domain;
using GymBackend.Models;
using GymBackend.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Security.Cryptography;
using System.Text;

namespace GymBackend.Data;

public static class DevelopmentSeeder
{
    public static async Task SeedAsync(IServiceProvider services, IHostEnvironment env, CancellationToken ct = default)
    {
        if (!env.IsDevelopment())
            throw new InvalidOperationException("Database:Initialize solo se permite en Development.");
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.EnsureCreatedAsync(ct);
        if (!await db.ScheduleSlots.AnyAsync(ct))
        {
            var days = GymWeekdays.OpeningDays;
            var ranges = new[] { (Start: "08:00", End: "09:30"), (Start: "09:30", End: "11:00"), (Start: "11:00", End: "12:30"), (Start: "13:00", End: "14:30") };
            db.ScheduleSlots.AddRange(days.SelectMany(day => ranges.Select(range => new ScheduleSlot
            {
                Day = day, StartTime = range.Start, EndTime = range.End,
                Zone = Zones.Default, Capacity = 20, Enabled = true,
            })));
            await db.SaveChangesAsync(ct);
        }
        var bootstrap = scope.ServiceProvider.GetRequiredService<IOptions<BootstrapOptions>>().Value;
        var adminPassword = bootstrap.AdminPassword;
        if (!await db.Users.AnyAsync(user => user.Role == Roles.Admin, ct) && !string.IsNullOrWhiteSpace(adminPassword))
        {
            var rut = bootstrap.AdminRut;
            var email = bootstrap.AdminEmail;
            var username = bootstrap.AdminUsername;
            if (adminPassword.Length < 16 || string.IsNullOrWhiteSpace(rut) || string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(username))
                throw new InvalidOperationException("Bootstrap requiere RUT, correo, usuario y una contraseña de al menos 16 caracteres.");
            // Kept compatible with the extracted AuthController for audit; not a hardened password scheme.
            db.Users.Add(new User
            {
                Rut = rut, Name = UserTypes.Administrator, Email = email.Trim().ToLowerInvariant(),
                Username = username.Trim().ToLowerInvariant(),
                PasswordHash = Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(adminPassword))),
                UserType = UserTypes.Administrator, Role = Roles.Admin, Enabled = true,
            });
            await db.SaveChangesAsync(ct);
        }
    }
}
