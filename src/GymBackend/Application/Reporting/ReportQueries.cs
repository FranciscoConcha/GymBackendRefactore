using GymBackend.Data;
using GymBackend.Domain;
using GymBackend.Models;
using GymBackend.Options;
using GymBackend.Time;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace GymBackend.Application.Reporting;

public sealed record ReportFilter(DateOnly? From = null, DateOnly? To = null, int? BranchId = null, string? Category = null)
{
    public ReportFilter Resolve(IGymClock clock) => this with { From = From ?? clock.Today.AddDays(-29), To = To ?? clock.Today };
    public bool IsValid => From <= To && (Category is null or UserCategories.Deportista or UserCategories.Estudiante
        or UserCategories.Funcionario or UserCategories.Externo);
}

public sealed class ReportQueries(AppDbContext db)
{
    public IQueryable<Reservation> Reservations(ReportFilter f) => db.Reservations.AsNoTracking()
        .Where(r => r.Date >= f.From && r.Date <= f.To
            && (!f.BranchId.HasValue || r.SportBranchIdAtBooking == f.BranchId)
            && (f.Category == null || r.CategoryAtBooking == f.Category));

    public IQueryable<GroupBooking> Groups(ReportFilter f) => db.GroupBookings.AsNoTracking()
        .Where(g => g.Date >= f.From && g.Date <= f.To && (!f.BranchId.HasValue || g.SportBranchId == f.BranchId)
            && (f.Category == null || f.Category == UserCategories.Deportista));

    public IQueryable<Visit> Visits(ReportFilter f) => db.Visits.AsNoTracking()
        .Where(v => v.Date >= f.From && v.Date <= f.To
            && (!f.BranchId.HasValue || (v.Reservation != null
                ? v.Reservation.SportBranchIdAtBooking == f.BranchId : v.GroupBooking!.SportBranchId == f.BranchId))
            && (f.Category == null || (v.Reservation != null
                ? v.Reservation.CategoryAtBooking == f.Category : v.GroupBooking != null && f.Category == UserCategories.Deportista)));

    public IQueryable<User> Users(ReportFilter f) => db.Users.AsNoTracking()
        .Where(u => (!f.BranchId.HasValue || u.SportBranchId == f.BranchId) && (f.Category == null || u.Category == f.Category));
}

public sealed class MetricsService(AppDbContext db, ReportQueries queries, IOptions<GymOptions> options)
{
    public async Task<object> GetAsync(ReportFilter f, CancellationToken ct)
    {
        var reservations = queries.Reservations(f);
        var groups = queries.Groups(f);
        var visits = queries.Visits(f);
        var counts = await reservations.GroupBy(r => r.Status).Select(g => new { Status = g.Key, Count = g.Count() }).ToArrayAsync(ct);
        int Count(ReservationStatus status) => counts.SingleOrDefault(c => c.Status == status)?.Count ?? 0;
        var demandHours = await reservations.GroupBy(r => r.ScheduleSlot.StartTime)
            .Select(g => new { StartTime = g.Key, Reservations = g.Count() }).OrderBy(g => g.StartTime).ToArrayAsync(ct);
        var branchCounts = await reservations.GroupBy(r => r.SportBranchIdAtBooking)
            .Select(g => new { BranchId = g.Key, Reservations = g.Count() }).ToArrayAsync(ct);
        var branchIds = branchCounts.Where(b => b.BranchId.HasValue).Select(b => b.BranchId!.Value).ToArray();
        var names = await db.SportBranches.Where(b => branchIds.Contains(b.Id)).ToDictionaryAsync(b => b.Id, b => b.Name, ct);
        // Every operation here is an aggregate in the database. Only grouped result sets are materialized.
        return new
        {
            period = new { f.From, f.To },
            filters = new { f.BranchId, f.Category },
            occupancy = new { current = await db.Visits.CountAsync(v => v.ExitAt == null, ct), max = options.Value.MaxOccupancy },
            reservations = new
            {
                total = counts.Sum(c => c.Count),
                attended = Count(ReservationStatus.Attended),
                cancelled = Count(ReservationStatus.Cancelled),
                noShow = Count(ReservationStatus.NoShow)
            },
            groupBookings = new
            {
                count = await groups.CountAsync(ct),
                seats = await groups.SumAsync(g => g.Seats, ct),
                usedSeats = await db.Visits.Where(v => v.GroupBookingId != null && groups.Select(g => g.Id).Contains(v.GroupBookingId.Value))
                    .Select(v => new { v.GroupBookingId, v.UserId }).Distinct().CountAsync(ct)
            },
            uniqueVisitors = await visits.Select(v => v.UserId).Distinct().CountAsync(ct),
            visits = await visits.CountAsync(ct),
            averageStayMinutes = await visits.Where(v => v.ExitAt != null).AverageAsync(v => (double?)(v.ExitAt!.Value - v.EntryAt).TotalMinutes, ct),
            demandByHour = demandHours.Select(h => new { startTime = h.StartTime.ToString("HH:mm"), h.Reservations }),
            demandByBranch = branchCounts.Select(b => new { b.BranchId, name = b.BranchId.HasValue ? names.GetValueOrDefault(b.BranchId.Value) : null, b.Reservations }),
            enabledUsersByCategory = await queries.Users(f).Where(u => u.Enabled && u.Category != null).GroupBy(u => u.Category)
                .Select(g => new { category = g.Key, count = g.Count() }).ToArrayAsync(ct),
        };
    }
}
