using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GymBackend.Data.Migrations
{
    /// <inheritdoc />
    public partial class PreloadCatalogAndCapacity30 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                CREATE TEMP TABLE preload_branches (name text) ON COMMIT DROP;
                INSERT INTO preload_branches (name) VALUES
                    ('Baloncesto varones'),
                    ('Baloncesto damas'),
                    ('Vóleibol damas'),
                    ('Vóleibol varones'),
                    ('Fútbol varones'),
                    ('Fútbol damas'),
                    ('Futsal varones'),
                    ('Futsal damas'),
                    ('Balonmano varones'),
                    ('Escalada'),
                    ('Ajedrez'),
                    ('Atletismo'),
                    ('Hockey'),
                    ('Gimnasia Artística'),
                    ('Judo'),
                    ('Karate'),
                    ('Montañismo'),
                    ('Levantamiento de pesas'),
                    ('Lucha de brazos'),
                    ('Natación'),
                    ('Taekwondo'),
                    ('Tenis de mesa'),
                    ('Rugby');
                INSERT INTO "SportBranches" ("Name", "Enabled", "CreatedAt")
                SELECT name, true, now() FROM preload_branches p
                WHERE NOT EXISTS (SELECT 1 FROM "SportBranches" b WHERE lower(b."Name") = lower(p.name));
                UPDATE "SportBranches" b SET "Enabled" = EXISTS
                    (SELECT 1 FROM preload_branches p WHERE lower(p.name) = lower(b."Name"));
                INSERT INTO "ScheduleSlots" ("Day", "StartTime", "EndTime", "Zone", "Capacity", "Enabled")
                SELECT day, start_time, end_time, 'Sala de Pesas', 30, true
                FROM generate_series(1, 6) AS day
                CROSS JOIN (VALUES (time '08:00', time '09:30'), (time '09:30', time '11:00'),
                    (time '11:00', time '12:30'), (time '13:00', time '14:30')) AS times(start_time, end_time)
                ON CONFLICT ("Day", "StartTime", "EndTime", "Zone") DO NOTHING;
                UPDATE "ScheduleSlots" SET "Capacity" = 30;
                DROP TABLE preload_branches;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Preserve catalog and associated data; only restore the previous capacity.
            migrationBuilder.Sql("UPDATE \"ScheduleSlots\" SET \"Capacity\" = 20;");
        }
    }
}
