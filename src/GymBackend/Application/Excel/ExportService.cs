using System.Globalization;
using ClosedXML.Excel;
using GymBackend.Application.Reporting;
using GymBackend.Data;
using GymBackend.Domain;
using GymBackend.Models;
using GymBackend.Time;
using Microsoft.EntityFrameworkCore;

namespace GymBackend.Application.Excel;

public sealed class ExportService(AppDbContext db, ReportQueries queries, IGymClock clock)
{
    public const int MaxRows = 100_000;

    public async Task<MemoryStream> CreateAsync(ReportFilter f, int actorId, CancellationToken ct)
    {
        using var workbook = new XLWorkbook();
        var filters = workbook.Worksheets.Add("Filtros");
        Write(filters, 1, ["Desde", "Hasta", "Rama", "Categoría", "Zona horaria"]);
        Write(filters, 2, [f.From, f.To, f.BranchId, f.Category, clock.TimeZone.Id]);
        await Sheet(workbook, "Usuarios", ["RUT", "Nombre", "Apellido", "Correo", "Rol", "Categoría", "Rama", "Estado"],
            queries.Users(f).Include(u => u.SportBranch).OrderBy(u => u.Id),
            u => [u.Rut, u.FirstName, u.LastName, u.Email, u.Role, u.Category, u.SportBranch?.Name,
                !u.Enabled ? "Deshabilitada" : u.AccountStatus.ToString()], ct);

        var sessions = workbook.Worksheets.Add("Sesiones");
        Write(sessions, 1, ["Fecha", "Inicio", "Fin", "Capacidad", "Recinto", "Reservas", "Cupos de grupo"]);
        var row = 1;
        var slots = await db.ScheduleSlots.AsNoTracking().OrderBy(s => s.Id).ToArrayAsync(ct);
        // Sessions have no branch/category of their own; their counts use the requested booking filters.
        for (var date = f.From!.Value; ; date = date.AddDays(1))
        {
            foreach (var slot in slots.Where(s => s.Day == date.DayOfWeek))
            {
                if (++row > MaxRows + 1) throw new InvalidDataException("Una hoja supera las 100 000 filas.");
                var reservations = await queries.Reservations(f).CountAsync(r => r.Date == date && r.ScheduleSlotId == slot.Id
                    && (r.Status == ReservationStatus.Reserved || r.Status == ReservationStatus.Attended), ct);
                var seats = await queries.Groups(f).Where(g => g.Date == date && g.ScheduleSlotId == slot.Id && g.Status == "Activa").SumAsync(g => g.Seats, ct);
                Write(sessions, row, [date, slot.StartTime, slot.EndTime, slot.Capacity, slot.Zone, reservations, seats]);
            }
            if (date == f.To!.Value) break;
        }
        var branchNames = await db.SportBranches.AsNoTracking().ToDictionaryAsync(b => b.Id, b => b.Name, ct);
        await Sheet(workbook, "Reservas", ["Id", "Titular", "RUT", "Fecha", "Inicio", "Estado", "Creada", "Categoría", "Rama"],
            queries.Reservations(f).Include(r => r.User).Include(r => r.ScheduleSlot).OrderBy(r => r.Id),
            r => [r.Id, r.User.FullName, r.User.Rut, r.Date, r.ScheduleSlot.StartTime, ReservationStatuses.Name(r.Status),
                Local(r.CreatedAt), r.CategoryAtBooking, r.SportBranchIdAtBooking.HasValue ? branchNames.GetValueOrDefault(r.SportBranchIdAtBooking.Value) : null], ct);
        await Sheet(workbook, "Grupos", ["Rama", "Fecha", "Horario", "Modo", "Cupos", "Usados", "Creador", "Estado"],
            queries.Groups(f).OrderBy(g => g.Id).Select(g => new
            {
                g.SportBranch.Name,
                g.Date,
                g.ScheduleSlot.StartTime,
                g.ScheduleSlot.EndTime,
                g.Mode,
                g.Seats,
                Used = db.Visits.Where(v => v.GroupBookingId == g.Id).Select(v => v.UserId).Distinct().Count(),
                g.CreatedBy.FirstName,
                g.CreatedBy.LastName,
                g.Status,
            }), g => [g.Name, g.Date, $"{g.StartTime:HH:mm}–{g.EndTime:HH:mm}", g.Mode, g.Seats, g.Used, $"{g.FirstName} {g.LastName}", g.Status], ct);
        await Sheet(workbook, "Visitas", ["Usuario", "RUT", "Entrada", "Salida", "Permanencia", "Documento", "Operador entrada", "Operador salida"],
            queries.Visits(f).Include(v => v.User).Include(v => v.EntryOperator).Include(v => v.ExitOperator).OrderBy(v => v.Id),
            v => [v.User.FullName, v.User.Rut, Local(v.EntryAt), v.ExitAt.HasValue ? Local(v.ExitAt.Value) : null,
                v.ExitAt.HasValue ? (v.ExitAt.Value - v.EntryAt).TotalMinutes : null, v.DocumentType, v.EntryOperator.FullName, v.ExitOperator?.FullName], ct);
        var reservationIds = queries.Reservations(f).Select(r => r.Id);
        await Sheet(workbook, "Inasistencias", ["Reserva", "Usuario", "Fecha", "Acuse"],
            db.AttendanceAlerts.AsNoTracking().Where(a => reservationIds.Contains(a.ReservationId))
                .Include(a => a.User).Include(a => a.Reservation).OrderBy(a => a.Id),
            a => [a.ReservationId, a.User.FullName, a.Reservation.Date,
                a.AcknowledgedAt.HasValue ? Local(a.AcknowledgedAt.Value.UtcDateTime) : null], ct);
        var stream = new MemoryStream();
        try
        {
            workbook.SaveAs(stream);
            stream.Position = 0;
            db.AuditEvents.Add(new AuditEvent
            {
                Action = "export.created",
                ActorUserId = actorId,
                OccurredAt = clock.UtcNow,
                TargetType = "Export",
                TargetId = Guid.NewGuid().ToString("N")
            });
            await db.SaveChangesAsync(ct);
            return stream;
        }
        catch { await stream.DisposeAsync(); throw; }
    }

    private DateTime Local(DateTime utc) => TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), clock.TimeZone);

    private static async Task Sheet<T>(XLWorkbook workbook, string name, string[] headers, IQueryable<T> query,
        Func<T, object?[]> values, CancellationToken ct)
    {
        var sheet = workbook.Worksheets.Add(name);
        Write(sheet, 1, headers.Cast<object?>().ToArray());
        var row = 1;
        await foreach (var item in query.Take(MaxRows + 1).AsAsyncEnumerable().WithCancellation(ct))
        {
            if (++row > MaxRows + 1) throw new InvalidDataException("Una hoja supera las 100 000 filas.");
            Write(sheet, row, values(item));
        }
    }

    private static void Write(IXLWorksheet sheet, int row, object?[] values)
    {
        for (var column = 0; column < values.Length; column++)
        {
            var cell = sheet.Cell(row, column + 1);
            cell.Value = values[column] switch
            {
                null => Blank.Value,
                DateTime date => date,
                DateOnly date => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                TimeOnly time => time.ToString("HH:mm", CultureInfo.InvariantCulture),
                int number => number,
                double number => number,
                var value => SpreadsheetSafety.Neutralize(Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty),
            };
            if (values[column] is DateTime) cell.Style.DateFormat.Format = "yyyy-mm-dd hh:mm:ss";
        }
    }
}
