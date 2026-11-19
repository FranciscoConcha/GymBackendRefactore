using GymBackend.Application.Email;
using GymBackend.Data;
using GymBackend.Models;
using GymBackend.Options;
using GymBackend.Time;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Testcontainers.PostgreSql;

namespace GymBackend.IntegrationTests.Auth;

public sealed class EmailOutboxTests
{
    [Fact]
    public async Task Dispatcher_DisabledLeavesPending_PickupWritesEml_AndRetriesStopAtFive()
    {
        // A separate database keeps this worker from consuming other tests' pending mail.
#pragma warning disable CS0618
        await using var postgres = new PostgreSqlBuilder().WithImage("postgres:17-alpine").Build();
#pragma warning restore CS0618
        await postgres.StartAsync(TestContext.Current.CancellationToken);
        var directory = Path.Combine(Path.GetTempPath(), $"gym-mail-{Guid.NewGuid():N}");
        var clock = new FakeTimeProvider(new DateTimeOffset(2026, 11, 18, 12, 0, 0, TimeSpan.Zero));
        var settings = new EmailOptions { Mode = EmailMode.Disabled, PickupDirectory = directory };
        var services = new ServiceCollection();
        services.AddDbContext<AppDbContext>(o => o.UseNpgsql(postgres.GetConnectionString()));
        services.AddSingleton<TimeProvider>(clock);
        services.AddSingleton<IGymClock>(new GymClock(clock, Microsoft.Extensions.Options.Options.Create(new GymOptions())));
        await using var provider = services.BuildServiceProvider();
        var dispatcher = new EmailOutboxDispatcher(provider.GetRequiredService<IServiceScopeFactory>(), Microsoft.Extensions.Options.Options.Create(settings), clock);
        try
        {
            await using (var scope = provider.CreateAsyncScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                await db.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);
                db.EmailOutbox.Add(new EmailOutbox
                {
                    To = "invitado@example.test",
                    Subject = "Activa tu cuenta",
                    Body = "Para activar tu cuenta, abre https://frontend.example/activar?token=ejemplo",
                    CreatedAt = clock.GetUtcNow(),
                    NextAttemptAt = clock.GetUtcNow(),
                });
                await db.SaveChangesAsync(TestContext.Current.CancellationToken);
            }
            await dispatcher.DispatchPendingAsync(TestContext.Current.CancellationToken);
            Assert.False(Directory.Exists(directory));
            await using (var scope = provider.CreateAsyncScope())
            {
                var mail = await scope.ServiceProvider.GetRequiredService<AppDbContext>().EmailOutbox.SingleAsync(TestContext.Current.CancellationToken);
                Assert.Null(mail.SentAt);
                Assert.Equal(0, mail.AttemptCount);
            }
            settings.Mode = EmailMode.Pickup;
            await dispatcher.DispatchPendingAsync(TestContext.Current.CancellationToken);
            var path = Assert.Single(Directory.GetFiles(directory, "*.eml"));
            var eml = await File.ReadAllTextAsync(path, TestContext.Current.CancellationToken);
            Assert.Contains("invitado@example.test", eml);
            Assert.Contains("text/plain", eml);
            await using var sentScope = provider.CreateAsyncScope();
            var sent = await sentScope.ServiceProvider.GetRequiredService<AppDbContext>().EmailOutbox.SingleAsync(TestContext.Current.CancellationToken);
            Assert.NotNull(sent.SentAt);
            Assert.Equal(1, sent.AttemptCount);
            Assert.Null(sent.LastError);

            var blockedPath = Path.Combine(directory, "blocked");
            await File.WriteAllTextAsync(blockedPath, "This is a file, not a pickup directory.", TestContext.Current.CancellationToken);
            settings.PickupDirectory = blockedPath;
            var retryDb = sentScope.ServiceProvider.GetRequiredService<AppDbContext>();
            var failing = new EmailOutbox
            {
                To = "retry@example.test",
                Subject = "Correo de prueba",
                Body = "Contenido que no debe aparecer en LastError",
                CreatedAt = clock.GetUtcNow(),
                NextAttemptAt = clock.GetUtcNow(),
            };
            retryDb.EmailOutbox.Add(failing);
            await retryDb.SaveChangesAsync(TestContext.Current.CancellationToken);
            for (var attempt = 1; attempt <= 5; attempt++)
            {
                var beforeAttempt = clock.GetUtcNow();
                await dispatcher.DispatchPendingAsync(TestContext.Current.CancellationToken);
                await retryDb.Entry(failing).ReloadAsync(TestContext.Current.CancellationToken);
                Assert.Equal(attempt, failing.AttemptCount);
                Assert.Null(failing.SentAt);
                Assert.Equal(beforeAttempt.AddMinutes(Math.Pow(2, attempt - 1)), failing.NextAttemptAt);
                Assert.Equal("No se pudo enviar el correo.", failing.LastError);
                Assert.DoesNotContain(failing.Body, failing.LastError!);
                clock.Advance(TimeSpan.FromMinutes(Math.Pow(2, attempt - 1)));
            }
            settings.PickupDirectory = directory;
            await dispatcher.DispatchPendingAsync(TestContext.Current.CancellationToken);
            await retryDb.Entry(failing).ReloadAsync(TestContext.Current.CancellationToken);
            Assert.Equal(5, failing.AttemptCount);
            Assert.Null(failing.SentAt);
            Assert.Single(Directory.GetFiles(directory, "*.eml"));
        }
        finally
        {
            dispatcher.Dispose();
            if (Directory.Exists(directory)) Directory.Delete(directory, true);
        }
    }
}
