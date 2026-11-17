using GymBackend.Domain;
using GymBackend.Data;
using GymBackend.Options;
using Microsoft.Extensions.Options;
using GymBackend.DTOs;
using GymBackend.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace GymBackend.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly IOptions<JwtOptions> _jwtOptions;

    public AuthController(AppDbContext context, IOptions<JwtOptions> jwtOptions)
    {
        _context = context;
        _jwtOptions = jwtOptions;
    }

    [HttpPost("register")]
    [Authorize(Roles = Roles.Admin)]
    [ProducesResponseType<UserMessageResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<MessageResponse>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Register(RegisterRequest request)
    {
        var rut = request.Rut.Trim();
        var name = request.Name.Trim();
        var email = request.Email.Trim().ToLowerInvariant();
        var userType = request.UserType.Trim();

        if (!IsValidRutFormat(rut))
        {
            return BadRequest(new MessageResponse("El RUT debe tener entre 8 y 9 dígitos, sin puntos ni guion."));
        }

        if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(request.Password))
        {
            return BadRequest(new MessageResponse("Todos los campos son obligatorios."));
        }

        if (userType.Equals(UserTypes.Administrator, StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest(new MessageResponse("Los administradores deben crearse por un proceso controlado."));
        }

        if (await _context.Users.AnyAsync(user => user.Rut == rut))
        {
            return BadRequest(new MessageResponse("El RUT ya está registrado en el sistema."));
        }

        if (await _context.Users.AnyAsync(user => user.Email.ToLower() == email))
        {
            return BadRequest(new MessageResponse("El correo ya está registrado en el sistema."));
        }

        var user = new User
        {
            Rut = rut,
            Name = name,
            Email = email,
            Username = string.Empty,
            PasswordHash = HashPassword(request.Password),
            UserType = userType,
            Role = Roles.Client,
        };

        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        return Ok(new UserMessageResponse("Usuario registrado correctamente.", UserResponse.From(user)));
    }

    [HttpPost("login")]
    [ProducesResponseType<AuthResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<MessageResponse>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<MessageResponse>(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Login(LoginRequest request)
    {
        var rut = request.Rut.Trim();

        if (!IsValidRutFormat(rut))
        {
            return BadRequest(new MessageResponse("El RUT debe tener entre 8 y 9 dígitos, sin puntos ni guion."));
        }

        if (string.IsNullOrWhiteSpace(request.Password))
        {
            return BadRequest(new MessageResponse("La contraseña es obligatoria."));
        }

        var passwordHash = HashPassword(request.Password);
        var user = await _context.Users.FirstOrDefaultAsync(candidate =>
            candidate.Rut == rut && candidate.PasswordHash == passwordHash);

        if (user is null || user.Role == Roles.Admin)
        {
            return Unauthorized(new MessageResponse("Credenciales de usuario incorrectas."));
        }

        return Ok(CreateAuthResponse(user, "Login correcto."));
    }

    [HttpPost("admin-login")]
    [ProducesResponseType<AuthResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<MessageResponse>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<MessageResponse>(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> AdminLogin(AdminLoginRequest request)
    {
        var username = request.Username.Trim().ToLowerInvariant();

        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(request.Password))
        {
            return BadRequest(new MessageResponse("El usuario y la contraseña son obligatorios."));
        }

        var passwordHash = HashPassword(request.Password);
        var admin = await _context.Users.FirstOrDefaultAsync(candidate =>
            candidate.Username.ToLower() == username &&
            candidate.PasswordHash == passwordHash &&
            candidate.Role == Roles.Admin);

        if (admin is null)
        {
            return Unauthorized(new MessageResponse("Credenciales de administrador incorrectas."));
        }

        return Ok(CreateAuthResponse(admin, "Login de administrador correcto."));
    }

    [HttpGet("users")]
    [Authorize(Roles = Roles.Admin)]
    [ProducesResponseType<UserResponse[]>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetUsers()
    {
        var users = (await _context.Users
            .OrderBy(user => user.Name)
            .ToListAsync())
            .Select(UserResponse.From);

        return Ok(users);
    }

    [HttpPatch("users/{id}/status")]
    [Authorize(Roles = Roles.Admin)]
    [ProducesResponseType<UserMessageResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<MessageResponse>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<MessageResponse>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SetUserStatus(int id, SetUserStatusRequest request)
    {
        var user = await _context.Users.FindAsync(id);

        if (user is null)
        {
            return NotFound(new MessageResponse("Usuario no encontrado."));
        }

        if (user.Role == Roles.Admin)
        {
            return BadRequest(new MessageResponse("No se puede deshabilitar a un administrador."));
        }

        user.Enabled = request.Enabled;
        await _context.SaveChangesAsync();

        return Ok(new UserMessageResponse(user.Enabled ? "Usuario habilitado correctamente." : "Usuario deshabilitado correctamente.", UserResponse.From(user)));
    }

    private AuthResponse CreateAuthResponse(User user, string message)
    {
        var key = _jwtOptions.Value.Key
            ?? throw new InvalidOperationException("Jwt:Key no está configurada.");

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Name, user.Name),
            new Claim(ClaimTypes.Role, user.Role),
            new Claim(GymClaimTypes.Rut, user.Rut),
            new Claim(GymClaimTypes.Email, user.Email),
        };

        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)),
            SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            claims: claims,
            expires: DateTime.UtcNow.AddHours(8),
            signingCredentials: credentials);

        return new AuthResponse(message, new JwtSecurityTokenHandler().WriteToken(token), UserResponse.From(user));
    }

    private static bool IsValidRutFormat(string rut)
    {
        return Regex.IsMatch(rut, @"^\d{8,9}$");
    }

    private static string HashPassword(string password)
    {
        using var sha256 = SHA256.Create();
        var bytes = Encoding.UTF8.GetBytes(password);
        var hash = sha256.ComputeHash(bytes);

        return Convert.ToBase64String(hash);
    }
}
