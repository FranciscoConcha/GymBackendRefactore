using GymBackend.Data.Migrations;
using GymBackend.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations.Operations;

namespace GymBackend.IntegrationTests.Smoke;

public sealed class CatalogMigrationTests(GymApiFactory factory)
{
    [Fact]
    public async Task MigrationIsIdempotent_PreservesLegacyData_AndDownOnlyRestoresCapacity()
    {
        await factory.WithDbAsync(async db =>
        {
            var ct = TestContext.Current.CancellationToken;
            await using var transaction = await db.Database.BeginTransactionAsync(ct);
            // Temporary tables shadow the real catalog on this connection only.
            await db.Database.ExecuteSqlRawAsync("""
                CREATE TEMP TABLE "SportBranches" (LIKE public."SportBranches" INCLUDING ALL) ON COMMIT DROP;
                CREATE TEMP TABLE "ScheduleSlots" (LIKE public."ScheduleSlots" INCLUDING ALL) ON COMMIT DROP;
                INSERT INTO "SportBranches" ("Name", "Enabled", "CreatedAt") VALUES
                    ('Legacy', true, now()), ('RUGBY', false, now());
                INSERT INTO "ScheduleSlots" ("Day", "StartTime", "EndTime", "Zone", "Capacity", "Enabled") VALUES
                    (0, '06:00', '07:00', 'Legacy', 5, false),
                    (1, '08:00', '09:30', 'Sala de Pesas', 20, true);
                """, ct);
            var migration = new PreloadCatalogAndCapacity30();
            foreach (var iteration in Enumerable.Range(0, 2))
            {
                foreach (var operation in migration.UpOperations.Cast<SqlOperation>())
                    await db.Database.ExecuteSqlRawAsync(operation.Sql, ct);
                var branches = await db.SportBranches.AsNoTracking().ToArrayAsync(ct);
                Assert.Equal(24, branches.Length);
                Assert.Equal(23, branches.Count(b => b.Enabled));
                Assert.False(Assert.Single(branches, b => b.Name == "Legacy").Enabled);
                Assert.True(Assert.Single(branches, b => b.Name == "RUGBY").Enabled);
                var expected = new[] { "Baloncesto varones", "Baloncesto damas", "V\u00f3leibol damas", "V\u00f3leibol varones",
                    "F\u00fatbol varones", "F\u00fatbol damas", "Futsal varones", "Futsal damas", "Balonmano varones",
                    "Escalada", "Ajedrez", "Atletismo", "Hockey", "Gimnasia Art\u00edstica", "Judo", "Karate",
                    "Monta\u00f1ismo", "Levantamiento de pesas", "Lucha de brazos", "Nataci\u00f3n", "Taekwondo", "Tenis de mesa", "Rugby" };
                Assert.Equal(expected.Select(n => n.ToLowerInvariant()).Order(), branches.Where(b => b.Enabled).Select(b => b.Name.ToLowerInvariant()).Order());
                var slots = await db.ScheduleSlots.AsNoTracking().ToArrayAsync(ct);
                Assert.Equal(25, slots.Length);
                Assert.All(slots, slot => Assert.Equal(30, slot.Capacity));
                var catalog = slots.Where(slot => slot.Zone == "Sala de Pesas").ToArray();
                Assert.Equal(24, catalog.Length);
                Assert.All(catalog, slot => Assert.True(slot.Enabled));
                foreach (var day in Enumerable.Range(1, 6))
                    Assert.Equal(new[] { (new TimeOnly(8, 0), new TimeOnly(9, 30)), (new TimeOnly(9, 30), new TimeOnly(11, 0)),
                        (new TimeOnly(11, 0), new TimeOnly(12, 30)), (new TimeOnly(13, 0), new TimeOnly(14, 30)) },
                        catalog.Where(slot => (int)slot.Day == day).OrderBy(slot => slot.StartTime).Select(slot => (slot.StartTime, slot.EndTime)));
            }
            foreach (var operation in migration.DownOperations.Cast<SqlOperation>())
                await db.Database.ExecuteSqlRawAsync(operation.Sql, ct);
            Assert.Equal(24, await db.SportBranches.CountAsync(ct));
            Assert.Equal(25, await db.ScheduleSlots.CountAsync(ct));
            Assert.All(await db.ScheduleSlots.AsNoTracking().ToArrayAsync(ct), slot => Assert.Equal(20, slot.Capacity));
            await transaction.RollbackAsync(ct);
        });
    }
}
