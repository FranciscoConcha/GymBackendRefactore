using System.Net;
using System.Net.Mail;
using System.Text;
using GymBackend.Data;
using GymBackend.Models;
using GymBackend.Options;
using GymBackend.Time;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace GymBackend.Application.Email;

public sealed class EmailOutboxDispatcher(IServiceScopeFactory scopes, IOptions<EmailOptions> options,
    TimeProvider timeProvider) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try { await DispatchPendingAsync(stoppingToken); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            // Do not pass exceptions to logs: transport/database messages can contain mail content.
            catch (Exception) { }
            try { await Task.Delay(TimeSpan.FromSeconds(10), timeProvider, stoppingToken); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
        }
    }

    public async Task DispatchPendingAsync(CancellationToken ct = default)
    {
        var settings = options.Value;
        if (settings.Mode == EmailMode.Disabled) return;
        for (var i = 0; i < 20; i++)
        {
            await using var scope = scopes.CreateAsyncScope();
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var clock = scope.ServiceProvider.GetRequiredService<IGymClock>();
            await using var transaction = await context.Database.BeginTransactionAsync(ct);
            var now = clock.UtcNow;
            // SKIP LOCKED prevents simultaneous workers sending the same row.
            var pending = await context.EmailOutbox.FromSqlInterpolated($"""
                SELECT * FROM "EmailOutbox"
                WHERE "SentAt" IS NULL AND "AttemptCount" < 5 AND "NextAttemptAt" <= {now}
                ORDER BY "Id" LIMIT 1 FOR UPDATE SKIP LOCKED
                """).ToListAsync(ct);
            if (pending.Count == 0) return;
            var email = pending[0];
            email.AttemptCount++;
            try
            {
                await SendAsync(email, settings, ct);
                email.SentAt = clock.UtcNow;
                email.LastError = null;
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
            catch (Exception)
            {
                email.LastError = "No se pudo enviar el correo.";
                email.NextAttemptAt = clock.UtcNow.AddMinutes(Math.Pow(2, email.AttemptCount - 1));
            }
            await context.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
        }
    }

    private static async Task SendAsync(EmailOutbox email, EmailOptions settings, CancellationToken ct)
    {
        using var message = new MailMessage(settings.From, email.To, email.Subject, email.Body)
        {
            IsBodyHtml = false,
            BodyEncoding = Encoding.UTF8,
            SubjectEncoding = Encoding.UTF8,
        };
        using var smtp = new SmtpClient();
        if (settings.Mode == EmailMode.Pickup)
        {
            Directory.CreateDirectory(settings.PickupDirectory);
            smtp.DeliveryMethod = SmtpDeliveryMethod.SpecifiedPickupDirectory;
            smtp.PickupDirectoryLocation = Path.GetFullPath(settings.PickupDirectory);
        }
        else
        {
            smtp.Host = settings.Host;
            smtp.Port = settings.Port;
            smtp.EnableSsl = settings.EnableSsl;
            smtp.DeliveryMethod = SmtpDeliveryMethod.Network;
            smtp.UseDefaultCredentials = false;
            if (settings.User.Length > 0) smtp.Credentials = new NetworkCredential(settings.User, settings.Password);
        }
        await smtp.SendMailAsync(message, ct);
    }
}
