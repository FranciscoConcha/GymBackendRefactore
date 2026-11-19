using System.Security.Cryptography;
using System.Text;
using GymBackend.Application.Audit;
using GymBackend.Application.Common;
using GymBackend.Data;
using GymBackend.Domain;
using GymBackend.DTOs;
using GymBackend.Models;
using GymBackend.Options;
using GymBackend.Time;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace GymBackend.Application.Auth;

public sealed class AccountService(AppDbContext context, IUserPasswordHasher hasher, IGymClock clock,
    IOptions<AccountsOptions> options, IAuditLog audit)
{
    public const string ResetRequestedMessage = "Si el correo está registrado, recibirás un enlace para restablecer tu contraseña.";
    public const string InvalidTokenMessage = "El enlace no es válido o expiró.";

    // The caller saves the token, email, user and audit together in its transaction.
    public void EnqueueInvitation(User user)
    {
        Enqueue(user, AccountTokenPurpose.Activation);
        audit.Add("user.invited", user);
    }

    public async Task<ServiceResult<MessageResponse>> InviteAsync(int id, CancellationToken ct = default)
    {
        await using var transaction = await context.Database.BeginTransactionAsync(ct);
        await LockUserAsync(id, ct);
        var user = await context.Users.SingleOrDefaultAsync(u => u.Id == id, ct);
        if (user is null) return ServiceError.NotFound("Usuario no encontrado.");
        if (user.AccountStatus != AccountStatus.Pendiente)
            return ServiceError.Conflict("La cuenta no está pendiente de activación.");
        EnqueueInvitation(user);
        await context.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return ServiceResult<MessageResponse>.Success(new("Invitación enviada correctamente."));
    }

    public async Task RequestResetAsync(string? email, CancellationToken ct = default)
    {
        var normalized = email?.Trim().ToLowerInvariant() ?? string.Empty;
        await using var transaction = await context.Database.BeginTransactionAsync(ct);
        var id = await context.Users.Where(u => u.Email.ToLower() == normalized)
            .Select(u => (int?)u.Id).SingleOrDefaultAsync(ct);
        if (id is null) return;
        await LockUserAsync(id.Value, ct);
        var user = await context.Users.SingleAsync(u => u.Id == id.Value, ct);
        if (!user.Enabled || user.AccountStatus != AccountStatus.Activa) return;
        var since = clock.UtcNow.AddHours(-1);
        if (await context.AccountTokens.CountAsync(t => t.UserId == user.Id
            && t.Purpose == AccountTokenPurpose.PasswordReset && t.CreatedAt > since, ct) >= 3) return;
        Enqueue(user, AccountTokenPurpose.PasswordReset);
        audit.Add("user.password_reset_requested", user);
        await context.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
    }

    public Task<ServiceResult<MessageResponse>> ActivateAsync(AccountPasswordRequest request, CancellationToken ct = default) =>
        ConsumeAsync(request, AccountTokenPurpose.Activation, ct);

    public Task<ServiceResult<MessageResponse>> ConfirmResetAsync(AccountPasswordRequest request, CancellationToken ct = default) =>
        ConsumeAsync(request, AccountTokenPurpose.PasswordReset, ct);

    private async Task<ServiceResult<MessageResponse>> ConsumeAsync(AccountPasswordRequest request, AccountTokenPurpose purpose, CancellationToken ct)
    {
        if (request.Token is null || request.Token.Length != 43)
            return ServiceError.Validation(InvalidTokenMessage);
        var hash = Hash(request.Token);
        await using var transaction = await context.Database.BeginTransactionAsync(ct);
        var id = await context.AccountTokens.Where(t => t.TokenHash == hash && t.Purpose == purpose)
            .Select(t => (int?)t.UserId).SingleOrDefaultAsync(ct);
        if (id is null) return ServiceError.Validation(InvalidTokenMessage);
        // All token creation and consumption is serialized per account, including sibling tokens.
        await LockUserAsync(id.Value, ct);
        var token = await context.AccountTokens.Include(t => t.User)
            .SingleAsync(t => t.TokenHash == hash && t.Purpose == purpose, ct);
        var user = token.User;
        var now = clock.UtcNow;
        if (token.UsedAt is not null || token.ExpiresAt <= now || !user.Enabled
            || user.AccountStatus != (purpose == AccountTokenPurpose.Activation ? AccountStatus.Pendiente : AccountStatus.Activa))
            return ServiceError.Validation(InvalidTokenMessage);
        if (request.NewPassword is null || request.NewPassword.Length is < 8 or > 128)
            return ServiceError.Validation("La nueva contraseña debe tener entre 8 y 128 caracteres.");
        user.PasswordHash = hasher.Hash(request.NewPassword);
        user.AccountStatus = AccountStatus.Activa;
        user.MustChangePassword = false;
        user.SessionVersion++;
        user.UpdatedAt = now;
        await context.AccountTokens.Where(t => t.UserId == user.Id && t.UsedAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.UsedAt, now), ct);
        audit.Add(purpose == AccountTokenPurpose.Activation ? "user.activated" : "user.password_reset", user);
        await context.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return ServiceResult<MessageResponse>.Success(new(purpose == AccountTokenPurpose.Activation
            ? "Cuenta activada correctamente." : "Contraseña restablecida correctamente."));
    }

    private void Enqueue(User user, AccountTokenPurpose purpose)
    {
        var token = WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(32));
        var activation = purpose == AccountTokenPurpose.Activation;
        var now = clock.UtcNow;
        context.AccountTokens.Add(new AccountToken
        {
            User = user,
            Purpose = purpose,
            TokenHash = Hash(token),
            CreatedAt = now,
            ExpiresAt = activation ? now.AddHours(options.Value.ActivationHours) : now.AddMinutes(options.Value.ResetMinutes),
        });
        var link = options.Value.FrontendBaseUrl.TrimEnd('/') + (activation ? "/activar?token=" : "/restablecer?token=") + token;
        context.EmailOutbox.Add(new EmailOutbox
        {
            To = user.Email,
            Subject = activation ? "Activa tu cuenta de UCN Gym" : "Restablece tu contraseña de UCN Gym",
            Body = (activation ? "Para activar tu cuenta y elegir tu contraseña, abre el siguiente enlace:"
                : "Para restablecer tu contraseña, abre el siguiente enlace:") + "\n\n" + link
                + "\n\nSi no solicitaste este correo, puedes ignorarlo.",
            CreatedAt = now,
            NextAttemptAt = now,
        });
    }

    private Task LockUserAsync(int id, CancellationToken ct) => context.Database.ExecuteSqlInterpolatedAsync(
        $"SELECT 1 FROM \"Users\" WHERE \"Id\" = {id} FOR UPDATE", ct);

    private static string Hash(string token) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}
