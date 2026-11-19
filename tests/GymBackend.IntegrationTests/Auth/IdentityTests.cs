using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using GymBackend.Application.Audit;
using GymBackend.Domain;
using GymBackend.IntegrationTests.Infrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using static GymBackend.IntegrationTests.Infrastructure.Api;
namespace GymBackend.IntegrationTests.Auth;

public sealed class IdentityTests(GymApiFactory factory)
{
    [Fact]
    public async Task EditUsername_NormalizesRequiresUniqueStaffNameAndClearsForUser()
    {
        var user = await factory.CreateClientUserAsync();
        using var client = user.Client;
        using var admin = await factory.CreateAdminClientAsync();
        var path = $"/api/v1/auth/users/{user.UserId}";
        await Expect(await admin.PatchAsJsonAsync(path, new { role = Roles.Recepcionista, category = (string?)null }, JsonOptions, Ct), HttpStatusCode.BadRequest);
        await Expect(await admin.PatchAsJsonAsync(path, new { role = Roles.Recepcionista, category = (string?)null, username = new string('a', 65) }, JsonOptions, Ct), HttpStatusCode.BadRequest);
        await Expect(await admin.PatchAsJsonAsync(path, new { role = Roles.Recepcionista, category = (string?)null, username = factory.AdminUsername.ToUpperInvariant() }, JsonOptions, Ct), HttpStatusCode.Conflict);
        var username = $"staff-{Guid.NewGuid():N}";
        var promoted = await Expect(await admin.PatchAsJsonAsync(path, new { role = Roles.Recepcionista, category = (string?)null, username = $" {username.ToUpperInvariant()} " }, JsonOptions, Ct), HttpStatusCode.OK);
        Assert.Equal(username, promoted.GetProperty("user").GetProperty("username").GetString());
        using var login = factory.CreateClient();
        await Expect(await login.PostAsJsonAsync("/api/v1/auth/admin-login", new { username, password = user.Password }, JsonOptions, Ct), HttpStatusCode.OK);
        var demoted = await Expect(await admin.PatchAsJsonAsync(path, new { role = Roles.Usuario, category = UserCategories.Externo }, JsonOptions, Ct), HttpStatusCode.OK);
        Assert.Equal(string.Empty, demoted.GetProperty("user").GetProperty("username").GetString());
    }

    private static object Registration(string role = Roles.Usuario, string? category = UserCategories.Estudiante,
        int? sportBranchId = null, string? username = null) => new
        {
            rut = TestData.NextRut(),
            firstName = "Identity",
            lastName = "Test",
            email = $"identity-{Guid.NewGuid():N}@example.test",
            password = "Password123!",
            role,
            category,
            sportBranchId,
            username,
        };

    private static async Task<int> Branch(HttpClient admin, bool enabled = true)
    {
        var result = await Expect(await admin.PostAsJsonAsync("/api/v1/branches", new { name = $"Branch {Guid.NewGuid():N}" }, JsonOptions, Ct), HttpStatusCode.OK);
        var id = result.GetProperty("branch").GetProperty("id").GetInt32();
        if (!enabled) await Expect(await admin.PatchAsJsonAsync($"/api/v1/branches/{id}", new { enabled }, JsonOptions, Ct), HttpStatusCode.OK);
        return id;
    }

    [Fact]
    public async Task Receptionist_UsernameIsUniqueAndRoleChangesApplyToExistingSessions()
    {
        var receptionist = await factory.CreateReceptionistClientAsync();
        using var staff = receptionist.Client;
        using var admin = await factory.CreateAdminClientAsync();
        await Expect(await admin.PostAsJsonAsync("/api/v1/auth/register",
            Registration(Roles.Recepcionista, null, username: $" {receptionist.Username.ToUpperInvariant()} "), JsonOptions, Ct), HttpStatusCode.Conflict);
        var changed = await Expect(await admin.PatchAsJsonAsync($"/api/v1/auth/users/{receptionist.UserId}",
            new { role = Roles.Usuario, category = UserCategories.Externo }, JsonOptions, Ct), HttpStatusCode.OK);
        Assert.Equal(Roles.Usuario, changed.GetProperty("user").GetProperty("role").GetString());
        using var entry = await Api.Access(staff, "entry", "invalid");
        Assert.Equal(HttpStatusCode.Forbidden, entry.StatusCode);
        // Categories do not grant access to staff operations.
        using var users = await staff.GetAsync("/api/v1/auth/users", Ct);
        Assert.Equal(HttpStatusCode.Forbidden, users.StatusCode);
        await Expect(await admin.PatchAsJsonAsync($"/api/v1/auth/users/{receptionist.UserId}",
            new { role = Roles.Recepcionista, username = receptionist.Username, category = (string?)null, sportBranchId = (int?)null }, JsonOptions, Ct), HttpStatusCode.OK);
        var me = await Expect(await staff.GetAsync("/api/v1/auth/me", Ct), HttpStatusCode.OK);
        Assert.Equal(Roles.Recepcionista, me.GetProperty("role").GetString());
        Assert.Equal(JsonValueKind.Null, me.GetProperty("category").ValueKind);
    }

    [Fact]
    public async Task IdentityTimestampsAndAudit_UseConfiguredClock()
    {
        using var realAdmin = await factory.CreateAdminClientAsync();
        var clock = new FakeTimeProvider(new DateTimeOffset(2026, 11, 18, 12, 0, 0, TimeSpan.Zero));
        using var host = factory.WithClock(clock);
        using var admin = host.CreateClient();
        admin.DefaultRequestHeaders.Authorization = realAdmin.DefaultRequestHeaders.Authorization;
        var user = (await Expect(await admin.PostAsJsonAsync("/api/v1/auth/register", Registration(), JsonOptions, Ct), HttpStatusCode.OK)).GetProperty("user");
        var id = user.GetProperty("id").GetInt32();
        Assert.Equal(clock.GetUtcNow(), user.GetProperty("createdAt").GetDateTimeOffset());
        Assert.Equal(clock.GetUtcNow(), user.GetProperty("updatedAt").GetDateTimeOffset());
        clock.Advance(TimeSpan.FromMinutes(1));
        var edited = (await Expect(await admin.PatchAsJsonAsync($"/api/v1/auth/users/{id}", new { lastName = "Updated" }, JsonOptions, Ct), HttpStatusCode.OK)).GetProperty("user");
        Assert.Equal(user.GetProperty("createdAt").GetDateTimeOffset(), edited.GetProperty("createdAt").GetDateTimeOffset());
        Assert.Equal(clock.GetUtcNow(), edited.GetProperty("updatedAt").GetDateTimeOffset());
        var targetId = id.ToString(CultureInfo.InvariantCulture);
        var audit = await factory.WithDbAsync(db => db.AuditEvents.SingleAsync(a => a.TargetType == "User" && a.TargetId == targetId && a.Action == "user.updated", Ct));
        Assert.Equal(clock.GetUtcNow(), audit.OccurredAt);
    }

    [Fact]
    public async Task AuditFailure_RollsBackBusinessChanges()
    {
        var rut = TestData.NextRut();
        var clock = factory.Services.GetRequiredService<GymBackend.Time.IGymClock>();
        await Assert.ThrowsAsync<DbUpdateException>(() => factory.WithDbAsync(async db =>
        {
            var user = new GymBackend.Models.User
            {
                Rut = rut,
                FirstName = "Rollback",
                LastName = "Test",
                Email = $"{rut}@example.test",
                Category = UserCategories.Estudiante,
                CreatedAt = clock.UtcNow,
                UpdatedAt = clock.UtcNow,
            };
            db.Users.Add(user);
            var log = new AuditLog(db, clock, new HttpContextAccessor());
            // The audit insert exceeds its configured column size, after the user insert.
            log.Add(new string('a', 81), user);
            await db.SaveChangesAsync(Ct);
        }));
        Assert.False(await factory.WithDbAsync(db => db.Users.AnyAsync(u => u.Rut == rut, Ct)));
    }

    [Fact]
    public async Task Registration_EnforcesIdentityCoherence()
    {
        using var admin = await factory.CreateAdminClientAsync();
        var disabled = await Branch(admin, false);
        var enabled = await Branch(admin);
        foreach (var payload in new[]
        {
            Registration(category: UserCategories.Deportista),
            Registration(category: UserCategories.Deportista, sportBranchId: disabled),
            Registration(category: UserCategories.Deportista, sportBranchId: int.MaxValue),
            Registration(sportBranchId: enabled),
            Registration(category: null),
            Registration(role: Roles.Recepcionista, category: null),
            Registration(role: Roles.Recepcionista, username: "reception"),
            Registration(role: Roles.Recepcionista, category: null, sportBranchId: enabled, username: "reception"),
            Registration(role: Roles.Admin, category: null, username: "admin"),
        }) await Expect(await admin.PostAsJsonAsync("/api/v1/auth/register", payload, JsonOptions, Ct), HttpStatusCode.BadRequest);
        var user = (await Expect(await admin.PostAsJsonAsync("/api/v1/auth/register",
            Registration(category: UserCategories.Deportista, sportBranchId: enabled), JsonOptions, Ct), HttpStatusCode.OK)).GetProperty("user");
        Assert.Equal(enabled, user.GetProperty("sportBranch").GetProperty("id").GetInt32());
        Assert.Equal("Identity Test", user.GetProperty("fullName").GetString());
    }

    [Fact]
    public async Task Receptionist_DisabledSessionAndStaffLoginAreRejected()
    {
        var receptionist = await factory.CreateReceptionistClientAsync();
        using var staff = receptionist.Client;
        using var admin = await factory.CreateAdminClientAsync();
        await Expect(await Status(admin, receptionist.UserId, false), HttpStatusCode.OK);
        using var me = await staff.GetAsync("/api/v1/auth/me", Ct);
        Assert.Equal(HttpStatusCode.Unauthorized, me.StatusCode);
        using var anonymous = factory.CreateClient();
        await Expect(await anonymous.PostAsJsonAsync("/api/v1/auth/admin-login",
            new { username = receptionist.Username, password = receptionist.Password }, JsonOptions, Ct), HttpStatusCode.Unauthorized,
            "Credenciales de administrador incorrectas.");
    }

    [Theory]
    [InlineData("firstName")]
    [InlineData("lastName")]
    public async Task Registration_NamesAreLimitedToEightyCharacters(string field)
    {
        using var admin = await factory.CreateAdminClientAsync();
        var rut = TestData.NextRut();
        var body = new Dictionary<string, object?>
        {
            ["rut"] = rut,
            ["email"] = $"{rut}@example.test",
            ["password"] = "Password123!",
            ["firstName"] = "First",
            ["lastName"] = "Last",
            ["role"] = Roles.Usuario,
            ["category"] = UserCategories.Estudiante,
        };
        body[field] = new string('a', 81);
        await Expect(await admin.PostAsJsonAsync("/api/v1/auth/register", body, JsonOptions, Ct), HttpStatusCode.BadRequest);
        body[field] = new string('a', 80);
        await Expect(await admin.PostAsJsonAsync("/api/v1/auth/register", body, JsonOptions, Ct), HttpStatusCode.OK);
    }

    [Theory]
    [InlineData("firstName", "<script>")]
    [InlineData("lastName", "<script>")]
    [InlineData("email", "invalid")]
    public async Task Edit_InvalidField_IsRejected(string field, string value)
    {
        var user = await factory.CreateClientUserAsync();
        using var client = user.Client;
        using var admin = await factory.CreateAdminClientAsync();
        await Expect(await admin.PatchAsJsonAsync($"/api/v1/auth/users/{user.UserId}", new Dictionary<string, object?> { [field] = value }, JsonOptions, Ct), HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Receptionist_PermissionsAndLoginMatrix()
    {
        var receptionist = await factory.CreateReceptionistClientAsync();
        using var staff = receptionist.Client;
        using var anonymous = factory.CreateClient();
        await Expect(await anonymous.PostAsJsonAsync("/api/v1/auth/login", new { rut = receptionist.Rut, password = receptionist.Password }, JsonOptions, Ct), HttpStatusCode.Unauthorized, "Credenciales de usuario incorrectas.");
        var login = await Expect(await anonymous.PostAsJsonAsync("/api/v1/auth/admin-login", new { username = $" {receptionist.Username.ToUpperInvariant()} ", password = receptionist.Password }, JsonOptions, Ct), HttpStatusCode.OK);
        Assert.Equal(Roles.Recepcionista, login.GetProperty("user").GetProperty("role").GetString());
        await Expect(await staff.GetAsync("/api/v1/auth/me", Ct), HttpStatusCode.OK);
        await Expect(await staff.GetAsync("/api/v1/schedules", Ct), HttpStatusCode.OK);
        await Expect(await staff.GetAsync("/api/v1/branches", Ct), HttpStatusCode.OK);
        var own = await Expect(await staff.GetAsync($"/api/v1/reservations/user/{receptionist.UserId}", Ct), HttpStatusCode.OK);
        Assert.Empty(own.EnumerateArray());
        using var users = await staff.GetAsync("/api/v1/auth/users", Ct);
        Assert.Equal(HttpStatusCode.Forbidden, users.StatusCode);
        using var register = await staff.PostAsJsonAsync("/api/v1/auth/register", Registration(), JsonOptions, Ct);
        Assert.Equal(HttpStatusCode.Forbidden, register.StatusCode);
        using var update = await staff.PatchAsJsonAsync($"/api/v1/auth/users/{receptionist.UserId}", new { firstName = "Changed" }, JsonOptions, Ct);
        Assert.Equal(HttpStatusCode.Forbidden, update.StatusCode);
        using var status = await Status(staff, receptionist.UserId, false);
        Assert.Equal(HttpStatusCode.Forbidden, status.StatusCode);
        using var all = await staff.GetAsync("/api/v1/reservations", Ct);
        Assert.Equal(HttpStatusCode.Forbidden, all.StatusCode);
        using var other = await staff.GetAsync("/api/v1/reservations/user/0", Ct);
        Assert.Equal(HttpStatusCode.Forbidden, other.StatusCode);
        using var reserve = await Reserve(staff, 1, GymTime.Tomorrow());
        Assert.Equal(HttpStatusCode.Forbidden, reserve.StatusCode);
        using var createBranch = await staff.PostAsJsonAsync("/api/v1/branches", new { name = "Forbidden" }, JsonOptions, Ct);
        Assert.Equal(HttpStatusCode.Forbidden, createBranch.StatusCode);
        using var patchBranch = await staff.PatchAsJsonAsync("/api/v1/branches/1", new { enabled = false }, JsonOptions, Ct);
        Assert.Equal(HttpStatusCode.Forbidden, patchBranch.StatusCode);
        // Use a fixed clock and a genuine reservation/QR to verify successful entry and exit.
        var clock = new FakeTimeProvider(new DateTimeOffset(2026, 11, 17, 13, 0, 0, TimeSpan.Zero));
        using var host = factory.WithClock(clock);
        using var timedStaff = host.CreateClient();
        timedStaff.DefaultRequestHeaders.Authorization = staff.DefaultRequestHeaders.Authorization;
        var user = await factory.CreateClientUserAsync();
        using var userClient = user.Client;
        using var timedUser = host.CreateClient();
        timedUser.DefaultRequestHeaders.Authorization = userClient.DefaultRequestHeaders.Authorization;
        var slot = await factory.CreateSlotAsync(DayOfWeek.Tuesday, new TimeOnly(10, 0), new TimeOnly(11, 0));
        await factory.InsertReservationAsync(user.UserId, slot.Id, new DateOnly(2026, 11, 17));
        var qr = await Api.Qr(timedUser);
        var entry = await Expect(await Api.Access(timedStaff, "entry", qr), HttpStatusCode.OK);
        Assert.Equal($"Test {user.Rut}", entry.GetProperty("visit").GetProperty("fullName").GetString());
        await Expect(await Api.Access(timedStaff, "exit", qr), HttpStatusCode.OK);
        await Expect(await staff.PostAsync("/api/v1/auth/logout", null, Ct), HttpStatusCode.OK);
    }

    [Fact]
    public async Task Branches_CreateUpdateDuplicateVisibilityAndAuthorization()
    {
        using var admin = await factory.CreateAdminClientAsync();
        var name = $"Sport {Guid.NewGuid():N}";
        var created = await Expect(await admin.PostAsJsonAsync("/api/v1/branches", new { name }, JsonOptions, Ct), HttpStatusCode.OK);
        var id = created.GetProperty("branch").GetProperty("id").GetInt32();
        await Expect(await admin.PostAsJsonAsync("/api/v1/branches", new { name = $" {name.ToUpperInvariant()} " }, JsonOptions, Ct), HttpStatusCode.Conflict);
        var second = await Branch(admin);
        await Expect(await admin.PatchAsJsonAsync($"/api/v1/branches/{second}", new { name = name.ToUpperInvariant() }, JsonOptions, Ct), HttpStatusCode.Conflict);
        await Expect(await admin.PatchAsJsonAsync("/api/v1/branches/2147483647", new { enabled = false }, JsonOptions, Ct), HttpStatusCode.NotFound);
        await Expect(await admin.PostAsJsonAsync("/api/v1/branches", new { name = "<script>" }, JsonOptions, Ct), HttpStatusCode.BadRequest);
        await Expect(await admin.PostAsJsonAsync("/api/v1/branches", new { name = new string('a', 81) }, JsonOptions, Ct), HttpStatusCode.BadRequest);
        var updated = await Expect(await admin.PatchAsJsonAsync($"/api/v1/branches/{id}", new { name = name + " Updated", enabled = false }, JsonOptions, Ct), HttpStatusCode.OK);
        Assert.False(updated.GetProperty("branch").GetProperty("enabled").GetBoolean());
        var all = await Expect(await admin.GetAsync("/api/v1/branches", Ct), HttpStatusCode.OK);
        Assert.Contains(all.EnumerateArray(), b => b.GetProperty("id").GetInt32() == id);
        var user = await factory.CreateClientUserAsync();
        using var client = user.Client;
        var receptionist = await factory.CreateReceptionistClientAsync();
        using var staff = receptionist.Client;
        foreach (var authenticated in new[] { client, staff })
        {
            var visible = await Expect(await authenticated.GetAsync("/api/v1/branches", Ct), HttpStatusCode.OK);
            Assert.DoesNotContain(visible.EnumerateArray(), b => b.GetProperty("id").GetInt32() == id);
            Assert.All(visible.EnumerateArray(), b => Assert.True(b.GetProperty("enabled").GetBoolean()));
        }
        using var anonymous = factory.CreateClient();
        using var denied = await anonymous.GetAsync("/api/v1/branches", Ct);
        Assert.Equal(HttpStatusCode.Unauthorized, denied.StatusCode);
    }

    [Fact]
    public async Task Edit_FiltersAndAudit_ContainOnlyFieldNames()
    {
        using var admin = await factory.CreateAdminClientAsync();
        var branchId = await Branch(admin);
        var user = await factory.CreateClientUserAsync();
        using var client = user.Client;
        var prefix = $"Identity{Guid.NewGuid():N}";
        var updated = await Expect(await admin.PatchAsJsonAsync($"/api/v1/auth/users/{user.UserId}", new
        {
            firstName = prefix,
            lastName = "Zulu",
            email = $"{prefix}@example.test",
            category = UserCategories.Deportista,
            sportBranchId = branchId,
        }, JsonOptions, Ct), HttpStatusCode.OK);
        var responseUser = updated.GetProperty("user");
        Assert.Equal(prefix + " Zulu", responseUser.GetProperty("fullName").GetString());
        Assert.Equal(branchId, responseUser.GetProperty("sportBranch").GetProperty("id").GetInt32());
        Assert.True(responseUser.GetProperty("updatedAt").GetDateTimeOffset() >= responseUser.GetProperty("createdAt").GetDateTimeOffset());
        foreach (var search in new[] { prefix.ToUpperInvariant(), "ZULU", user.Rut, $"{prefix.ToUpperInvariant()}@EXAMPLE.TEST" })
        {
            var matches = await Expect(await admin.GetAsync($"/api/v1/auth/users?search={Uri.EscapeDataString(search)}&role=Usuario&category=Deportista&branchId={branchId}&enabled=true", Ct), HttpStatusCode.OK);
            Assert.Equal(user.UserId, Assert.Single(matches.EnumerateArray()).GetProperty("id").GetInt32());
        }
        foreach (var filter in new[] { "role=Recepcionista", "category=Externo", "branchId=2147483647", "enabled=false" })
        {
            var matches = await Expect(await admin.GetAsync($"/api/v1/auth/users?search={prefix}&{filter}", Ct), HttpStatusCode.OK);
            Assert.Empty(matches.EnumerateArray());
        }
        await Expect(await admin.PatchAsJsonAsync($"/api/v1/auth/users/{user.UserId}", new { category = UserCategories.Estudiante }, JsonOptions, Ct), HttpStatusCode.BadRequest);
        await Expect(await admin.PatchAsJsonAsync($"/api/v1/auth/users/{user.UserId}", new { category = UserCategories.Estudiante, sportBranchId = (int?)null }, JsonOptions, Ct), HttpStatusCode.OK);
        using var promote = await admin.PatchAsJsonAsync($"/api/v1/auth/users/{user.UserId}", new { role = Roles.Admin }, JsonOptions, Ct);
        Assert.Equal(HttpStatusCode.Forbidden, promote.StatusCode);
        var adminId = await factory.WithDbAsync(db => db.Users.Where(u => u.Rut == factory.AdminRut).Select(u => u.Id).SingleAsync(Ct));
        using var editAdmin = await admin.PatchAsJsonAsync($"/api/v1/auth/users/{adminId}", new { firstName = "Changed" }, JsonOptions, Ct);
        Assert.Equal(HttpStatusCode.Forbidden, editAdmin.StatusCode);
        await Expect(await admin.PatchAsJsonAsync("/api/v1/auth/users/2147483647", new { firstName = "Changed" }, JsonOptions, Ct), HttpStatusCode.NotFound);
        await Expect(await Status(admin, user.UserId, false), HttpStatusCode.OK);
        var target = user.UserId.ToString(CultureInfo.InvariantCulture);
        var audits = await factory.WithDbAsync(db => db.AuditEvents.Where(a => a.TargetType == "User" && a.TargetId == target).OrderBy(a => a.Id).ToArrayAsync(Ct));
        Assert.Equal(new[] { "user.created", "user.updated", "user.updated", "user.status_changed" }, audits.Select(a => a.Action));
        Assert.All(audits, a => { Assert.Equal(adminId, a.ActorUserId); Assert.Equal(TimeSpan.Zero, a.OccurredAt.Offset); });
        Assert.Equal(new[] { "firstName", "lastName", "email", "category", "sportBranchId" }, JsonSerializer.Deserialize<string[]>(audits[1].Details));
        Assert.Equal(new[] { "enabled" }, JsonSerializer.Deserialize<string[]>(audits[3].Details));
        Assert.All(audits, a =>
        {
            Assert.DoesNotContain(user.Password, a.Details);
            Assert.DoesNotContain(prefix, a.Details);
            Assert.DoesNotContain("password", a.Details, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("token", a.Details, StringComparison.OrdinalIgnoreCase);
        });
        var branchTarget = branchId.ToString(CultureInfo.InvariantCulture);
        await Expect(await admin.PatchAsJsonAsync($"/api/v1/branches/{branchId}", new { enabled = false }, JsonOptions, Ct), HttpStatusCode.OK);
        var branchAudits = await factory.WithDbAsync(db => db.AuditEvents.Where(a => a.TargetType == "SportBranch" && a.TargetId == branchTarget).OrderBy(a => a.Id).ToArrayAsync(Ct));
        Assert.Equal(new[] { "branch.created", "branch.updated" }, branchAudits.Select(a => a.Action));
    }
}
