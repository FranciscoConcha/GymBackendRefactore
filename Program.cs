using GymBackend.Data;
using GymBackend.Models;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Security.Cryptography;
using System.Text;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddControllers();

var jwtKey = builder.Configuration["Jwt:Key"];
if (string.IsNullOrWhiteSpace(jwtKey) || jwtKey.Length < 32)
    throw new InvalidOperationException("Configure Jwt:Key con al menos 32 caracteres.");

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
if (string.IsNullOrWhiteSpace(connectionString))
    throw new InvalidOperationException("Configure ConnectionStrings:DefaultConnection.");

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
            ValidateIssuer = false,
            ValidateAudience = false,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromMinutes(1),
        };
    });
builder.Services.AddAuthorization();
builder.Services.AddDbContext<AppDbContext>(options => options.UseNpgsql(connectionString));
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
var origins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
builder.Services.AddCors(options => options.AddPolicy("Frontend", policy =>
{
    if (origins.Length > 0) policy.WithOrigins(origins).AllowAnyHeader().AllowAnyMethod();
}));

var app = builder.Build();
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
app.UseCors("Frontend");
if (!app.Environment.IsDevelopment()) app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapGet("/health/live", () => Results.Ok(new { status = "ok" })).AllowAnonymous();
app.MapGet("/health/ready", async (AppDbContext db) =>
{
    try { return await db.Database.CanConnectAsync() ? Results.Ok(new { status = "ok" }) : Results.StatusCode(503); }
    catch { return Results.StatusCode(503); }
}).AllowAnonymous();

// Isolated development bootstrap only. Production requires reviewed migrations.
if (builder.Configuration.GetValue<bool>("Database:Initialize"))
{
    if (!app.Environment.IsDevelopment())
        throw new InvalidOperationException("Database:Initialize solo se permite en Development.");
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.EnsureCreatedAsync();
    if (!await db.ScheduleSlots.AnyAsync())
    {
        var days = new[] { "Lunes", "Martes", "Miércoles", "Jueves", "Viernes", "Sábado" };
        var ranges = new[] { (Start: "08:00", End: "09:30"), (Start: "09:30", End: "11:00"), (Start: "11:00", End: "12:30"), (Start: "13:00", End: "14:30") };
        db.ScheduleSlots.AddRange(days.SelectMany(day => ranges.Select(range => new ScheduleSlot
        {
            Day = day, StartTime = range.Start, EndTime = range.End,
            Zone = "Sala de Pesas", Capacity = 20, Enabled = true,
        })));
        await db.SaveChangesAsync();
    }
    var adminPassword = builder.Configuration["Bootstrap:AdminPassword"];
    if (!await db.Users.AnyAsync(user => user.Role == "Admin") && !string.IsNullOrWhiteSpace(adminPassword))
    {
        var rut = builder.Configuration["Bootstrap:AdminRut"];
        var email = builder.Configuration["Bootstrap:AdminEmail"];
        var username = builder.Configuration["Bootstrap:AdminUsername"];
        if (adminPassword.Length < 16 || string.IsNullOrWhiteSpace(rut) || string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(username))
            throw new InvalidOperationException("Bootstrap requiere RUT, correo, usuario y una contraseña de al menos 16 caracteres.");
        // Kept compatible with the extracted AuthController for audit; not a hardened password scheme.
        db.Users.Add(new User
        {
            Rut = rut, Name = "Administrador", Email = email.Trim().ToLowerInvariant(),
            Username = username.Trim().ToLowerInvariant(),
            PasswordHash = Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(adminPassword))),
            UserType = "Administrador", Role = "Admin", Enabled = true,
        });
        await db.SaveChangesAsync();
    }
}
app.Run();
