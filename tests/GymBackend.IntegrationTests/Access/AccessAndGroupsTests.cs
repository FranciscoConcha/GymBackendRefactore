using System.Net;
using System.Net.Http.Json;
using System.Text;
using GymBackend.Application.Access;
using GymBackend.Application.Qr;
using GymBackend.Domain;
using GymBackend.IntegrationTests.Infrastructure;
using GymBackend.Models;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Time.Testing;
using static GymBackend.IntegrationTests.Infrastructure.Api;

// Occupancy is global, so these acceptance cases must not overlap other fixture users' entries.
[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace GymBackend.IntegrationTests.Access;

public sealed class AccessAndGroupsTests(GymApiFactory factory)
{
    [Fact]
    public async Task LastSeat_IndividualAndGroupShareTheSameLockAndCapacity()
    {
        await using var session = await Session.Create(factory);
        var slot = await session.Slot(new(12, 0), new(13, 0), 1);
        var branch = await session.Branch();
        var user = await session.User();
        using var client = session.Client(user);
        var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        async Task<HttpResponseMessage> Individual()
        {
            await start.Task.WaitAsync(Ct);
            return await Reserve(client, slot.Id, Session.Today);
        }
        async Task<HttpResponseMessage> Group()
        {
            await start.Task.WaitAsync(Ct);
            return await session.Group(branch.Id, slot.Id, "TotalDisponible", null);
        }
        var tasks = new[] { Individual(), Group() };
        start.SetResult();
        var responses = await Task.WhenAll(tasks);
        try
        {
            Assert.Single(responses, r => r.StatusCode == HttpStatusCode.OK);
            Assert.Single(responses, r => r.StatusCode == HttpStatusCode.Conflict);
        }
        finally { foreach (var response in responses) response.Dispose(); }
        var individual = await factory.WithDbAsync(db => db.Reservations.CountAsync(r => r.ScheduleSlotId == slot.Id, Ct));
        var groupSeats = await factory.WithDbAsync(db => db.GroupBookings.Where(g => g.ScheduleSlotId == slot.Id).SumAsync(g => g.Seats, Ct));
        Assert.Equal(1, individual + groupSeats);
    }

    [Fact]
    public async Task VisitHistoryIsOwnOrAdminFiltered_AndCurrentShowsTheOpenVisit()
    {
        await using var session = await Session.Create(factory);
        var branch = await session.Branch();
        var user = await session.User(branch.Id);
        var other = await session.User();
        var slot = await session.Slot();
        var reservation = await factory.InsertReservationAsync(user.Id, slot.Id, Session.Today);
        await factory.WithDbAsync(async db =>
        {
            var stored = await db.Reservations.SingleAsync(r => r.Id == reservation.Id, Ct);
            stored.SportBranchIdAtBooking = branch.Id;
            stored.CategoryAtBooking = UserCategories.Deportista;
            await db.SaveChangesAsync(Ct);
        });
        await Expect(await Api.Access(session.Admin, "entry", session.Token(user)), HttpStatusCode.OK);
        var current = await Expect(await session.Admin.GetAsync("/api/v1/access/current", Ct), HttpStatusCode.OK);
        var person = Assert.Single(current.GetProperty("people").EnumerateArray(), p => p.GetProperty("userId").GetInt32() == user.Id);
        Assert.Equal("11:00", person.GetProperty("sessionEndTime").GetString());
        var path = $"/api/v1/visits?from=2026-11-17&to=2026-11-17&userId={user.Id}&branchId={branch.Id}&category=Deportista";
        var history = await Expect(await session.Admin.GetAsync(path, Ct), HttpStatusCode.OK);
        Assert.Single(history.EnumerateArray());
        using var client = session.Client(user);
        var mine = await Expect(await client.GetAsync("/api/v1/me/visits", Ct), HttpStatusCode.OK);
        Assert.All(mine.EnumerateArray(), v => Assert.Equal(user.Id, v.GetProperty("userId").GetInt32()));
        using var outsider = session.Client(other);
        var empty = await Expect(await outsider.GetAsync($"/api/v1/me/visits?userId={user.Id}", Ct), HttpStatusCode.OK);
        Assert.Empty(empty.EnumerateArray());
        // Historical filters use the booking snapshot even if the person's branch/category changes.
        await factory.WithDbAsync(async db =>
        {
            var stored = await db.Users.SingleAsync(u => u.Id == user.Id, Ct);
            stored.Category = UserCategories.Estudiante;
            stored.SportBranchId = null;
            await db.SaveChangesAsync(Ct);
        });
        Assert.Single((await Expect(await session.Admin.GetAsync(path, Ct), HttpStatusCode.OK)).EnumerateArray());
    }

    [Fact]
    public async Task ValidateDoesNotEnter_EntryRequiresDocument_AndEveryAttemptIsRecorded()
    {
        await using var session = await Session.Create(factory);
        var user = await session.User();
        var slot = await session.Slot();
        await factory.InsertReservationAsync(user.Id, slot.Id, Session.Today);
        var validation = await Expect(await Api.Access(session.Admin, "validate", session.Token(user)), HttpStatusCode.OK);
        Assert.True(validation.GetProperty("canEnter").GetBoolean());
        Assert.False(validation.GetProperty("inside").GetBoolean());
        Assert.False(await factory.WithDbAsync(db => db.Visits.AnyAsync(v => v.UserId == user.Id, Ct)));
        await Expect(await session.Admin.PostAsJsonAsync("/api/v1/access/entry", new { token = session.Token(user) }, JsonOptions, Ct), HttpStatusCode.BadRequest);
        using var malformed = new StringContent("{", Encoding.UTF8, "application/json");
        await Expect(await session.Admin.PostAsync("/api/v1/access/entry", malformed, Ct), HttpStatusCode.BadRequest);
        await Expect(await Api.Access(session.Admin, "entry", session.Token(user)), HttpStatusCode.OK);
        var events = await factory.WithDbAsync(db => db.AccessEvents.Where(e => e.OperatorId == session.OperatorId).OrderBy(e => e.Id).ToArrayAsync(Ct));
        Assert.Equal(new[] { "Aceptado", "Rechazado", "Rechazado", "Aceptado" }, events.Select(e => e.Result));
        Assert.All(events.Where(e => e.Result == "Rechazado"), e => Assert.False(string.IsNullOrWhiteSpace(e.Reason)));
    }

    [Fact]
    public async Task ReentryPreservesEveryTimestamp_AndTemporaryExitDoesNotReleaseReservedSeat()
    {
        await using var session = await Session.Create(factory);
        var user = await session.User();
        var slot = await session.Slot(capacity: 1);
        var reservation = await factory.InsertReservationAsync(user.Id, slot.Id, Session.Today);
        var first = (await Expect(await Api.Access(session.Admin, "entry", session.Token(user)), HttpStatusCode.OK)).GetProperty("visit");
        await Expect(await Api.Access(session.Admin, "entry", session.Token(user)), HttpStatusCode.Conflict);
        session.Clock.Advance(TimeSpan.FromMinutes(1));
        var firstExit = (await Expect(await Api.Access(session.Admin, "exit", session.Token(user)), HttpStatusCode.OK)).GetProperty("visit");
        session.Clock.Advance(TimeSpan.FromMinutes(1));
        var second = (await Expect(await Api.Access(session.Admin, "entry", session.Token(user)), HttpStatusCode.OK)).GetProperty("visit");
        session.Clock.Advance(TimeSpan.FromMinutes(1));
        var secondExit = (await Expect(await Api.Access(session.Admin, "exit", session.Token(user)), HttpStatusCode.OK)).GetProperty("visit");
        Assert.NotEqual(first.GetProperty("id").GetInt32(), second.GetProperty("id").GetInt32());
        Assert.Equal(first.GetProperty("entryAt").GetDateTime(), firstExit.GetProperty("entryAt").GetDateTime());
        Assert.Equal(1, firstExit.GetProperty("stayMinutes").GetDouble());
        Assert.Equal(1, secondExit.GetProperty("stayMinutes").GetDouble());
        Assert.Equal(2, await factory.WithDbAsync(db => db.Visits.CountAsync(v => v.UserId == user.Id && v.ExitAt != null, Ct)));
        Assert.Equal(ReservationStatus.Attended, await factory.WithDbAsync(db => db.Reservations.Where(r => r.Id == reservation.Id).Select(r => r.Status).SingleAsync(Ct)));
        var schedules = await Expect(await session.Admin.GetAsync("/api/v1/schedules?date=2026-11-17", Ct), HttpStatusCode.OK);
        var own = Assert.Single(schedules.EnumerateArray(), s => s.GetProperty("id").GetInt32() == slot.Id);
        Assert.Equal(1, own.GetProperty("reservedCount").GetInt32());
        Assert.Equal(1, own.GetProperty("individualReservations").GetInt32());
    }

    [Fact]
    public async Task DisabledAccountCanExitAfterSessionEnd()
    {
        await using var session = await Session.Create(factory);
        var user = await session.User();
        var slot = await session.Slot();
        await factory.InsertReservationAsync(user.Id, slot.Id, Session.Today);
        var token = session.Token(user);
        await Expect(await Api.Access(session.Admin, "entry", token), HttpStatusCode.OK);
        await Expect(await Status(session.Admin, user.Id, false), HttpStatusCode.OK);
        session.Clock.Advance(TimeSpan.FromHours(3));
        await Expect(await Api.Access(session.Admin, "exit", token), HttpStatusCode.OK);
        await Expect(await Api.Access(session.Admin, "entry", token), HttpStatusCode.BadRequest, "El usuario no está habilitado.");
    }

    [Theory]
    [InlineData(59, HttpStatusCode.BadRequest)]
    [InlineData(60, HttpStatusCode.OK)]
    public async Task GroupLeadTime_HasExactSixtyMinuteBoundary(int minutes, HttpStatusCode expected)
    {
        await using var session = await Session.Create(factory);
        var branch = await session.Branch();
        var start = new TimeOnly(10, 0).AddMinutes(minutes);
        var slot = await session.Slot(start, start.AddHours(1));
        await Expect(await session.Group(branch.Id, slot.Id, "Cupos", 2), expected);
    }

    [Fact]
    public async Task ConcurrentGroupsCannotOverbook_AndTotalDisponibleTakesOnlyRemainingSeats()
    {
        await using var session = await Session.Create(factory);
        var branch = await session.Branch();
        var slot = await session.Slot(new(12, 0), new(13, 0), 5);
        var user = await session.User();
        await factory.InsertReservationAsync(user.Id, slot.Id, Session.Today);
        var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        async Task<HttpResponseMessage> Send()
        {
            await start.Task.WaitAsync(Ct);
            return await session.Group(branch.Id, slot.Id, "Cupos", 3);
        }
        var tasks = new[] { Send(), Send() };
        start.SetResult();
        var results = await Task.WhenAll(tasks);
        try
        {
            Assert.Single(results, r => r.StatusCode == HttpStatusCode.OK);
            Assert.Single(results, r => r.StatusCode == HttpStatusCode.Conflict);
        }
        finally { foreach (var response in results) response.Dispose(); }
        var total = await Expect(await session.Group(branch.Id, slot.Id, "TotalDisponible", null), HttpStatusCode.OK);
        Assert.Equal(1, total.GetProperty("groupBooking").GetProperty("seats").GetInt32());
        await Expect(await session.Group(branch.Id, slot.Id, "TotalDisponible", null), HttpStatusCode.Conflict);
        var schedules = await Expect(await session.Admin.GetAsync("/api/v1/schedules?date=2026-11-17", Ct), HttpStatusCode.OK);
        var own = Assert.Single(schedules.EnumerateArray(), s => s.GetProperty("id").GetInt32() == slot.Id);
        Assert.Equal(5, own.GetProperty("reservedCount").GetInt32());
        Assert.Equal(4, own.GetProperty("groupSeats").GetInt32());
        Assert.Equal(1, own.GetProperty("individualReservations").GetInt32());
        using var client = session.Client(user);
        await Expect(await Reserve(client, slot.Id, Session.Today), HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task GroupsConsumeDistinctPeople_AllowReentry_RejectOtherBranch_AndPreferIndividual()
    {
        await using var session = await Session.Create(factory);
        var branch = await session.Branch();
        var otherBranch = await session.Branch();
        var slot = await session.Slot(new(12, 0), new(13, 0), 3);
        var created = await Expect(await session.Group(branch.Id, slot.Id, "Cupos", 1), HttpStatusCode.OK);
        var groupId = created.GetProperty("groupBooking").GetProperty("id").GetInt32();
        var first = await session.User(branch.Id);
        var second = await session.User(branch.Id);
        var outsider = await session.User(otherBranch.Id);
        var individual = await session.User(branch.Id);
        await factory.InsertReservationAsync(individual.Id, slot.Id, Session.Today);
        session.Clock.Advance(TimeSpan.FromHours(2));
        var individualEntry = await Expect(await Api.Access(session.Admin, "entry", session.Token(individual)), HttpStatusCode.OK);
        Assert.Equal(System.Text.Json.JsonValueKind.Null, individualEntry.GetProperty("visit").GetProperty("groupBookingId").ValueKind);
        await Expect(await Api.Access(session.Admin, "entry", session.Token(outsider)), HttpStatusCode.BadRequest);
        await Expect(await Api.Access(session.Admin, "entry", session.Token(first)), HttpStatusCode.OK);
        await Expect(await Api.Access(session.Admin, "exit", session.Token(first)), HttpStatusCode.OK);
        await Expect(await Api.Access(session.Admin, "entry", session.Token(second)), HttpStatusCode.BadRequest);
        await Expect(await Api.Access(session.Admin, "entry", session.Token(first)), HttpStatusCode.OK);
        var groups = await Expect(await session.Admin.GetAsync("/api/v1/group-bookings?from=2026-11-17&to=2026-11-17", Ct), HttpStatusCode.OK);
        var group = Assert.Single(groups.EnumerateArray(), g => g.GetProperty("id").GetInt32() == groupId);
        Assert.Equal(1, group.GetProperty("usedSeats").GetInt32());
        Assert.Equal(2, await factory.WithDbAsync(db => db.Visits.CountAsync(v => v.GroupBookingId == groupId, Ct)));
        using var member = session.Client(first);
        var mine = await Expect(await member.GetAsync("/api/v1/me/group-bookings", Ct), HttpStatusCode.OK);
        Assert.Contains(mine.EnumerateArray(), g => g.GetProperty("id").GetInt32() == groupId);
        var qr = await Expect(await member.GetAsync("/api/v1/qr/my-code", Ct), HttpStatusCode.OK);
        Assert.Equal(groupId, qr.GetProperty("nextSession").GetProperty("groupBookingId").GetInt32());
        Assert.Equal(branch.Id, qr.GetProperty("sportBranch").GetProperty("id").GetInt32());
    }

    [Fact]
    public async Task GroupCancelIsBeforeStartOnly_AndAudited()
    {
        await using var session = await Session.Create(factory);
        var branch = await session.Branch();
        var slot = await session.Slot(new(12, 0), new(13, 0));
        var created = await Expect(await session.Group(branch.Id, slot.Id, "Cupos", 2), HttpStatusCode.OK);
        var id = created.GetProperty("groupBooking").GetProperty("id").GetInt32();
        await Expect(await session.Admin.PostAsync($"/api/v1/group-bookings/{id}/cancel", null, Ct), HttpStatusCode.OK);
        var second = await Expect(await session.Group(branch.Id, slot.Id, "Cupos", 2), HttpStatusCode.OK);
        session.Clock.Advance(TimeSpan.FromHours(2));
        await Expect(await session.Admin.PostAsync($"/api/v1/group-bookings/{second.GetProperty("groupBooking").GetProperty("id").GetInt32()}/cancel", null, Ct), HttpStatusCode.BadRequest);
        var audits = await factory.WithDbAsync(db => db.AuditEvents.Where(a => a.ActorUserId == session.OperatorId).OrderBy(a => a.Id).ToArrayAsync(Ct));
        Assert.Equal(new[] { "group.created", "group.cancelled", "group.created" }, audits.Select(a => a.Action));
    }

    [Fact]
    public async Task ThirtyFirstPersonIsRejected_ConcurrentLastPlaceAdmitsExactlyOne()
    {
        await using var session = await Session.Create(factory);
        var slot = await session.Slot(capacity: 40);
        // Other tests may have left open visits; fill the remaining global occupancy to 29.
        var baseline = await factory.WithDbAsync(db => db.Visits.CountAsync(v => v.ExitAt == null, Ct));
        Assert.InRange(baseline, 0, 29);
        for (var i = baseline; i < 29; i++)
        {
            var user = await session.User();
            var reservation = await factory.InsertReservationAsync(user.Id, slot.Id, Session.Today);
            await factory.WithDbAsync(async db =>
            {
                db.Visits.Add(new Visit
                {
                    UserId = user.Id,
                    ReservationId = reservation.Id,
                    Date = Session.Today,
                    EntryAt = session.Clock.GetUtcNow().UtcDateTime,
                    EntryOperatorId = session.OperatorId,
                    DocumentType = "TNE"
                });
                await db.SaveChangesAsync(Ct);
            });
        }
        var candidates = new[] { await session.User(), await session.User() };
        foreach (var user in candidates) await factory.InsertReservationAsync(user.Id, slot.Id, Session.Today);
        var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var tasks = candidates.Select(async user =>
        {
            await start.Task.WaitAsync(Ct);
            return await Api.Access(session.Admin, "entry", session.Token(user));
        }).ToArray();
        start.SetResult();
        var responses = await Task.WhenAll(tasks);
        try
        {
            Assert.Single(responses, r => r.StatusCode == HttpStatusCode.OK);
            var rejected = Assert.Single(responses, r => r.StatusCode == HttpStatusCode.Conflict);
            Assert.Equal(AccessRules.Full, (await Read(rejected)).GetProperty("message").GetString());
        }
        finally { foreach (var response in responses) response.Dispose(); }
        Assert.Equal(30, await factory.WithDbAsync(db => db.Visits.CountAsync(v => v.ExitAt == null, Ct)));
        var another = await session.User();
        await factory.InsertReservationAsync(another.Id, slot.Id, Session.Today);
        await Expect(await Api.Access(session.Admin, "entry", session.Token(another)), HttpStatusCode.Conflict, AccessRules.Full);
    }

    [Fact]
    public async Task ReceptionistCanValidateEnterExit_ButCannotReadHistoryOrManageGroups()
    {
        await using var session = await Session.Create(factory);
        var receptionist = await factory.CreateReceptionistClientAsync();
        using var original = receptionist.Client;
        using var staff = session.Host.CreateClient();
        staff.DefaultRequestHeaders.Authorization = original.DefaultRequestHeaders.Authorization;
        var user = await session.User();
        var slot = await session.Slot();
        await factory.InsertReservationAsync(user.Id, slot.Id, Session.Today);
        foreach (var action in new[] { "validate", "entry", "exit" })
            await Expect(await Api.Access(staff, action, session.Token(user)), HttpStatusCode.OK);
        using var visits = await staff.GetAsync("/api/v1/visits", Ct);
        Assert.Equal(HttpStatusCode.Forbidden, visits.StatusCode);
        using var groups = await staff.GetAsync("/api/v1/group-bookings", Ct);
        Assert.Equal(HttpStatusCode.Forbidden, groups.StatusCode);
        using var create = await staff.PostAsJsonAsync("/api/v1/group-bookings", new { }, JsonOptions, Ct);
        Assert.Equal(HttpStatusCode.Forbidden, create.StatusCode);
        using var cancel = await staff.PostAsync("/api/v1/group-bookings/1/cancel", null, Ct);
        Assert.Equal(HttpStatusCode.Forbidden, cancel.StatusCode);
    }

    [Fact]
    public async Task TokensInvalidExpiredOrManipulatedCannotEnter()
    {
        await using var session = await Session.Create(factory);
        var user = await session.User();
        var slot = await session.Slot();
        await factory.InsertReservationAsync(user.Id, slot.Id, Session.Today);
        foreach (var token in new[] { "invalid", QrTokenFormat.Create(user.Rut, factory.QrKey, Session.Today.AddDays(-1)),
            QrTokenFormat.Create(user.Rut, "wrong-key", Session.Today), session.Token(user) + "tampered" })
            await Expect(await Api.Access(session.Admin, "entry", token), HttpStatusCode.BadRequest);
        var other = await session.User();
        await Expect(await Api.Access(session.Admin, "entry", session.Token(other)), HttpStatusCode.BadRequest);
        Assert.False(await factory.WithDbAsync(db => db.Visits.AnyAsync(v => v.UserId == user.Id, Ct)));
    }

    [Fact]
    public async Task IdempotencyReplaysAtomically_RejectsChangedBodyOrEndpoint_AndExpiresAfter24Hours()
    {
        await using var session = await Session.Create(factory);
        var branch = await session.Branch();
        var slot = await session.Slot(new(12, 0), new(13, 0));
        var key = Guid.NewGuid().ToString("N");
        var first = await Expect(await session.Group(branch.Id, slot.Id, "Cupos", 2, key), HttpStatusCode.OK);
        var second = await Expect(await session.Group(branch.Id, slot.Id, "Cupos", 2, key), HttpStatusCode.OK);
        Assert.Equal(first.GetRawText(), second.GetRawText());
        await Expect(await session.Group(branch.Id, slot.Id, "Cupos", 3, key), HttpStatusCode.UnprocessableEntity,
            "La clave de idempotencia ya se usó con otra solicitud.");
        using var missing = await session.Admin.PostAsJsonAsync("/api/v1/group-bookings", new
        {
            sportBranchId = branch.Id,
            scheduleSlotId = slot.Id,
            date = Session.Today,
            mode = "Cupos",
            seats = 1
        }, JsonOptions, Ct);
        Assert.Equal(HttpStatusCode.BadRequest, missing.StatusCode);
        Assert.Equal(1, await factory.WithDbAsync(db => db.GroupBookings.CountAsync(g => g.CreatedById == session.OperatorId, Ct)));
        var user = await session.User();
        using var client = session.Client(user);
        var reservationKey = Guid.NewGuid().ToString("N");
        var request = new { scheduleSlotId = slot.Id, date = Session.Today };
        var reservations = await Task.WhenAll(Send(client, "/api/v1/reservations", request, reservationKey), Send(client, "/api/v1/reservations", request, reservationKey));
        try
        {
            Assert.All(reservations, r => Assert.Equal(HttpStatusCode.OK, r.StatusCode));
            Assert.Equal(await reservations[0].Content.ReadAsStringAsync(Ct), await reservations[1].Content.ReadAsStringAsync(Ct));
        }
        finally { foreach (var response in reservations) response.Dispose(); }
        Assert.Equal(1, await factory.WithDbAsync(db => db.Reservations.CountAsync(r => r.UserId == user.Id, Ct)));
        session.Clock.Advance(TimeSpan.FromHours(2));
        var entryKey = Guid.NewGuid().ToString("N");
        var entry = new { token = session.Token(user), documentType = "TNE" };
        var entries = await Task.WhenAll(Send(session.Admin, "/api/v1/access/entry", entry, entryKey), Send(session.Admin, "/api/v1/access/entry", entry, entryKey));
        try
        {
            Assert.All(entries, r => Assert.Equal(HttpStatusCode.OK, r.StatusCode));
            Assert.Equal(await entries[0].Content.ReadAsStringAsync(Ct), await entries[1].Content.ReadAsStringAsync(Ct));
        }
        finally { foreach (var response in entries) response.Dispose(); }
        Assert.Equal(1, await factory.WithDbAsync(db => db.Visits.CountAsync(v => v.UserId == user.Id, Ct)));
        await Expect(await Send(session.Admin, "/api/v1/access/entry", entry, key), HttpStatusCode.UnprocessableEntity);
        await factory.WithDbAsync(async db =>
        {
            db.IdempotencyRecords.Add(new IdempotencyRecord
            {
                UserId = session.OperatorId,
                Key = "expired",
                Method = "POST",
                Path = "/old",
                RequestHash = "old",
                StatusCode = 200,
                ResponseBody = "{}",
                CreatedAt = session.Clock.GetUtcNow().AddHours(-25)
            });
            await db.SaveChangesAsync(Ct);
        });
        await Expect(await Send(session.Admin, "/api/v1/access/entry", entry, entryKey), HttpStatusCode.OK);
        Assert.False(await factory.WithDbAsync(db => db.IdempotencyRecords.AnyAsync(r => r.UserId == session.OperatorId && r.Key == "expired", Ct)));
    }

    [Fact]
    public async Task AccessRateLimitIsPerOperatorAndAllowsSixtyAttempts()
    {
        await using var session = await Session.Create(factory);
        for (var i = 0; i < 60; i++) await Expect(await Api.Access(session.Admin, "validate", "invalid"), HttpStatusCode.BadRequest);
        await Expect(await Api.Access(session.Admin, "validate", "invalid"), HttpStatusCode.TooManyRequests);
        var other = await factory.CreateDedicatedAdminClientAsync(session.Host);
        using var admin = other.Client;
        await Expect(await Api.Access(admin, "validate", "invalid"), HttpStatusCode.BadRequest);
    }

    private static async Task<HttpResponseMessage> Send(HttpClient client, string path, object body, string key)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, path) { Content = JsonContent.Create(body, options: JsonOptions) };
        request.Headers.Add("Idempotency-Key", key);
        return await client.SendAsync(request, Ct);
    }

    private sealed class Session(GymApiFactory factory, WebApplicationFactory<Program> host, HttpClient admin, int operatorId, FakeTimeProvider clock) : IAsyncDisposable
    {
        internal static readonly DateOnly Today = new(2026, 11, 17);
        private readonly List<int> _users = [];
        internal HttpClient Admin => admin;
        internal int OperatorId => operatorId;
        internal FakeTimeProvider Clock => clock;
        internal WebApplicationFactory<Program> Host => host;

        internal static async Task<Session> Create(GymApiFactory factory)
        {
            var clock = new FakeTimeProvider(new DateTimeOffset(2026, 11, 17, 13, 0, 0, TimeSpan.Zero));
            var host = factory.WithClock(clock);
            var admin = await factory.CreateDedicatedAdminClientAsync(host);
            return new(factory, host, admin.Client, admin.UserId, clock);
        }

        internal Task<ScheduleSlot> Slot(TimeOnly? start = null, TimeOnly? end = null, int capacity = 20) =>
            factory.CreateSlotAsync(Today.DayOfWeek, start ?? new(10, 0), end ?? new(11, 0), capacity);

        internal Task<SportBranch> Branch() => factory.WithDbAsync(async db =>
        {
            var branch = new SportBranch { Name = Guid.NewGuid().ToString("N"), CreatedAt = clock.GetUtcNow() };
            db.SportBranches.Add(branch);
            await db.SaveChangesAsync(Ct);
            return branch;
        });

        internal Task<User> User(int? branch = null) => factory.WithDbAsync(async db =>
        {
            var user = new User
            {
                Rut = TestData.NextRut(),
                FirstName = "Access",
                LastName = "Test",
                Email = $"{Guid.NewGuid():N}@example.test",
                Category = branch.HasValue ? UserCategories.Deportista : UserCategories.Estudiante,
                SportBranchId = branch,
                CreatedAt = clock.GetUtcNow(),
                UpdatedAt = clock.GetUtcNow(),
                PasswordHash = "unused"
            };
            db.Users.Add(user);
            await db.SaveChangesAsync(Ct);
            _users.Add(user.Id);
            return user;
        });

        internal string Token(User user) => QrTokenFormat.Create(user.Rut, factory.QrKey, Today);
        internal HttpClient Client(User user)
        {
            var client = host.CreateClient();
            client.DefaultRequestHeaders.Authorization = new("Bearer", factory.CreateJwt(user.Id, user.Role, user.Rut));
            return client;
        }

        internal Task<HttpResponseMessage> Group(int branchId, int slotId, string mode, int? seats, string? key = null) =>
            Send(admin, "/api/v1/group-bookings", new { sportBranchId = branchId, scheduleSlotId = slotId, date = Today, mode, seats }, key ?? Guid.NewGuid().ToString("N"));

        public async ValueTask DisposeAsync()
        {
            await factory.WithDbAsync(db => db.Visits.Where(v => _users.Contains(v.UserId) && v.ExitAt == null)
                .ExecuteUpdateAsync(s => s.SetProperty(v => v.ExitAt, v => v.EntryAt), Ct));
            admin.Dispose();
            await host.DisposeAsync();
        }
    }
}
