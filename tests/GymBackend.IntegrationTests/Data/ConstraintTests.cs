using GymBackend.Domain;
using GymBackend.IntegrationTests.Infrastructure;
using GymBackend.Models;
using Microsoft.EntityFrameworkCore;
using static GymBackend.IntegrationTests.Infrastructure.Api;

namespace GymBackend.IntegrationTests.Data;

public sealed class ConstraintTests(GymApiFactory factory)
{
    private static User NewUser() => new()
    {
        Rut = TestData.NextRut(), Name = "Constraint test",
        Email = $"constraint-{Guid.NewGuid():N}@example.test",
        Username = "", PasswordHash = "test-hash",
    };

    [Theory]
    [InlineData("Rut")]
    [InlineData("Email")]
    [InlineData("Username")]
    public async Task User_DuplicateIdentifier_ThrowsDbUpdateException(string property)
    {
        var first = NewUser();
        first.Username = $"constraint-{Guid.NewGuid():N}";
        await factory.WithDbAsync(async db =>
        {
            db.Users.Add(first);
            await db.SaveChangesAsync(Ct);
        });
        var second = NewUser();
        switch (property)
        {
            case "Rut": second.Rut = first.Rut; break;
            case "Email": second.Email = first.Email; break;
            case "Username": second.Username = first.Username; break;
        }
        await Assert.ThrowsAsync<DbUpdateException>(() => factory.WithDbAsync(async db =>
        {
            db.Users.Add(second);
            await db.SaveChangesAsync(Ct);
        }));
    }

    [Fact]
    public async Task User_TwoEmptyUsernames_AreAllowed()
    {
        var ids = await factory.WithDbAsync(async db =>
        {
            var first = NewUser();
            var second = NewUser();
            db.Users.AddRange(first, second);
            await db.SaveChangesAsync(Ct);
            return new[] { first.Id, second.Id };
        });
        Assert.NotEqual(ids[0], ids[1]);
        Assert.Equal(2, await factory.WithDbAsync(db => db.Users.CountAsync(u => ids.Contains(u.Id) && u.Username == "", Ct)));
    }

    [Fact]
    public async Task Reservation_TwoActiveForSameUserSlotDate_ThrowsDbUpdateException()
    {
        var user = await factory.CreateClientUserAsync();
        using var client = user.Client;
        var date = GymTime.Tomorrow();
        var slot = await factory.CreateSlotAsync(date.DayOfWeek, new TimeOnly(8, 0), new TimeOnly(9, 0));
        await factory.InsertReservationAsync(user.UserId, slot.Id, date);
        await Assert.ThrowsAsync<DbUpdateException>(() => factory.InsertReservationAsync(user.UserId, slot.Id, date));
    }

    [Fact]
    public async Task Reservation_CancelledAndNewActive_AreAllowed()
    {
        var user = await factory.CreateClientUserAsync();
        using var client = user.Client;
        var date = GymTime.Tomorrow();
        var slot = await factory.CreateSlotAsync(date.DayOfWeek, new TimeOnly(8, 0), new TimeOnly(9, 0));
        await factory.InsertReservationAsync(user.UserId, slot.Id, date, ReservationStatus.Cancelled);
        await factory.InsertReservationAsync(user.UserId, slot.Id, date);
        Assert.Equal(2, await factory.WithDbAsync(db => db.Reservations.CountAsync(r =>
            r.UserId == user.UserId && r.ScheduleSlotId == slot.Id && r.Date == date, Ct)));
    }

    [Fact]
    public async Task Attendance_DuplicateUserDate_ThrowsDbUpdateException()
    {
        var user = await factory.CreateClientUserAsync();
        using var client = user.Client;
        var date = GymTime.Tomorrow();
        await factory.WithDbAsync(async db =>
        {
            db.Attendances.Add(new GymBackend.Models.Attendance { UserId = user.UserId, Date = date });
            await db.SaveChangesAsync(Ct);
        });
        await Assert.ThrowsAsync<DbUpdateException>(() => factory.WithDbAsync(async db =>
        {
            db.Attendances.Add(new GymBackend.Models.Attendance { UserId = user.UserId, Date = date });
            await db.SaveChangesAsync(Ct);
        }));
    }

    [Fact]
    public async Task Schedule_ZeroCapacity_ThrowsDbUpdateException()
        => await Assert.ThrowsAsync<DbUpdateException>(() => factory.CreateSlotAsync(
            DayOfWeek.Monday, new TimeOnly(8, 0), new TimeOnly(9, 0), 0));

    [Theory]
    [InlineData(8)]
    [InlineData(7)]
    public async Task Schedule_EndNotAfterStart_ThrowsDbUpdateException(int endHour)
        => await Assert.ThrowsAsync<DbUpdateException>(() => factory.CreateSlotAsync(
            DayOfWeek.Monday, new TimeOnly(8, 0), new TimeOnly(endHour, 0)));

    [Fact]
    public async Task Schedule_DuplicateDayTimesAndZone_ThrowsDbUpdateException()
    {
        var slot = await factory.CreateSlotAsync(DayOfWeek.Monday, new TimeOnly(8, 0), new TimeOnly(9, 0));
        await Assert.ThrowsAsync<DbUpdateException>(() => factory.WithDbAsync(async db =>
        {
            db.ScheduleSlots.Add(new ScheduleSlot
            {
                Day = slot.Day, StartTime = slot.StartTime, EndTime = slot.EndTime,
                Zone = slot.Zone, Capacity = slot.Capacity,
            });
            await db.SaveChangesAsync(Ct);
        }));
    }
}
