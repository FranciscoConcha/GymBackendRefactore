using System.Text.Json;
using ClosedXML.Excel;
using GymBackend.Application.Auth;
using GymBackend.Application.Common;
using GymBackend.Data;
using GymBackend.Domain;
using GymBackend.Models;
using GymBackend.Time;
using Microsoft.EntityFrameworkCore;

namespace GymBackend.Application.Excel;

public sealed record ImportSummary(int Create, int Skip, int Error);
public sealed record ImportPreview(int Id, DateTimeOffset ExpiresAt, ImportSummary Summary, List<UserImportRow> Rows);
public sealed record ImportResult(int Created, int Skipped, int Failed, List<UserImportRow> Rows);

public sealed class UserImportService(AppDbContext db, IGymClock clock, AccountService accounts)
{
    public const int MaxBytes = 2 * 1024 * 1024;
    public const int MaxRows = 1000;
    private static readonly string[] Headers = ["rut", "nombre", "apellido", "correo", "categoria", "rama"];

    public async Task<ImportPreview> PreviewAsync(Stream stream, int actorId, CancellationToken ct)
    {
        var rows = Read(stream);
        rows = await ValidateAsync(rows, ct);
        var import = new UserImport
        {
            CreatedById = actorId,
            CreatedAt = clock.UtcNow,
            ExpiresAt = clock.UtcNow.AddHours(1),
            PayloadJson = JsonSerializer.Serialize(rows)
        };
        db.UserImports.Add(import);
        await db.SaveChangesAsync(ct);
        return new(import.Id, import.ExpiresAt, new(rows.Count(r => r.Action == "Crear"), rows.Count(r => r.Action == "Omitir"),
            rows.Count(r => r.Action == "Error")), rows);
    }

    public static List<UserImportRow> Read(Stream stream)
    {
        try { return ReadWorkbook(stream); }
        catch (Exception exception) when (exception is not (OutOfMemoryException or OperationCanceledException))
        {
            throw new InvalidDataException("El archivo no es un .xlsx válido o no cumple los límites.", exception);
        }
    }

    private static List<UserImportRow> ReadWorkbook(Stream stream)
    {
        using var workbook = new XLWorkbook(stream);
        if (workbook.Worksheets.Count == 0) throw new InvalidDataException("El archivo debe contener una hoja de usuarios.");
        var sheet = workbook.Worksheet(1);
        var last = sheet.LastRowUsed(XLCellsUsedOptions.AllContents)?.RowNumber() ?? 0;
        if (last < 2 || last - 1 > MaxRows) throw new InvalidDataException("El archivo debe contener entre 1 y 1000 filas.");
        var lastColumn = sheet.LastColumnUsed(XLCellsUsedOptions.AllContents)?.ColumnNumber() ?? 0;
        var columns = new Dictionary<string, int>();
        for (var col = 1; col <= lastColumn; col++)
        {
            var cell = sheet.Cell(1, col);
            if (cell.HasFormula) throw new InvalidDataException("Los encabezados no admiten fórmulas.");
            var name = UserImportRows.NormalizeHeader(cell.GetString());
            if (Headers.Contains(name) && !columns.TryAdd(name, col)) throw new InvalidDataException("Encabezado duplicado.");
        }
        if (Headers.Any(h => !columns.ContainsKey(h))) throw new InvalidDataException("Faltan encabezados obligatorios.");
        var rows = new List<UserImportRow>();
        for (var row = 2; row <= last; row++)
        {
            var hasFormula = sheet.Row(row).CellsUsed(XLCellsUsedOptions.AllContents).Any(c => c.HasFormula);
            string Text(string header)
            {
                var cell = sheet.Cell(row, columns[header]);
                return SpreadsheetSafety.Neutralize(cell.HasFormula ? "=" + cell.FormulaA1 : cell.GetString());
            }
            rows.Add(new(row, Text("rut"), Text("nombre"), Text("apellido"), Text("correo"), Text("categoria"), Text("rama"), hasFormula));
        }
        return rows;
    }

    private async Task<List<UserImportRow>> ValidateAsync(List<UserImportRow> rows, CancellationToken ct)
    {
        var normalizedRuts = rows.Select(r => RutFormat.TryNormalize(r.Rut, out var rut) ? rut : r.Rut).ToArray();
        var emails = rows.Select(r => r.Email.Trim().ToLowerInvariant()).ToArray();
        var existing = await db.Users.Where(u => normalizedRuts.Contains(u.Rut) || emails.Contains(u.Email.ToLower()))
            .Select(u => new { u.Rut, u.Email }).ToArrayAsync(ct);
        return UserImportRows.Validate(rows, await db.SportBranches.AsNoTracking().ToArrayAsync(ct),
            existing.Select(u => u.Rut).ToHashSet(), existing.Select(u => u.Email.ToLowerInvariant()).ToHashSet());
    }

    public async Task<ServiceResult<ImportResult>> ConfirmAsync(int id, int actorId, CancellationToken ct)
    {
        // The idempotency filter may already own the transaction.
        await using var transaction = db.Database.CurrentTransaction is null ? await db.Database.BeginTransactionAsync(ct) : null;
        var import = await db.UserImports.FromSqlInterpolated($"SELECT * FROM \"UserImports\" WHERE \"Id\" = {id} FOR UPDATE")
            .SingleOrDefaultAsync(ct);
        if (import is null) return ServiceError.NotFound("Importación no encontrada.");
        if (import.Status == "Confirmada") return ServiceResult<ImportResult>.Success(JsonSerializer.Deserialize<ImportResult>(import.ResultJson!)!);
        if (import.Status == "Expirada" || import.ExpiresAt <= clock.UtcNow)
        {
            import.Status = "Expirada";
            await db.SaveChangesAsync(ct);
            if (transaction is not null) await transaction.CommitAsync(ct);
            return ServiceError.Conflict("La vista previa expiró.");
        }
        // Serialize imports against all user inserts, including registrations, before revalidation.
        await db.Database.ExecuteSqlRawAsync("LOCK TABLE \"Users\" IN SHARE ROW EXCLUSIVE MODE", ct);
        await db.Database.ExecuteSqlRawAsync("SELECT 1 FROM \"SportBranches\" FOR SHARE", ct);
        var rows = await ValidateAsync(JsonSerializer.Deserialize<List<UserImportRow>>(import.PayloadJson)!, ct);
        foreach (var row in rows.Where(r => r.Action == "Crear"))
        {
            var branch = row.Branch.Length == 0 ? null : await db.SportBranches.SingleAsync(b => b.Name.ToLower() == row.Branch.ToLower(), ct);
            var user = new User
            {
                Rut = row.Rut,
                FirstName = row.FirstName,
                LastName = row.LastName,
                Email = row.Email,
                Category = row.Category,
                SportBranchId = branch?.Id,
                Role = Roles.Usuario,
                AccountStatus = AccountStatus.Pendiente,
                PasswordHash = "",
                Enabled = true,
                CreatedAt = clock.UtcNow,
                UpdatedAt = clock.UtcNow
            };
            db.Users.Add(user);
            accounts.EnqueueInvitation(user);
        }
        var result = new ImportResult(rows.Count(r => r.Action == "Crear"), rows.Count(r => r.Action == "Omitir"), rows.Count(r => r.Action == "Error"), rows);
        import.Status = "Confirmada";
        import.ResultJson = JsonSerializer.Serialize(result);
        db.AuditEvents.Add(new AuditEvent
        {
            Action = "import.confirmed",
            ActorUserId = actorId,
            OccurredAt = clock.UtcNow,
            TargetType = nameof(UserImport),
            TargetId = id.ToString(System.Globalization.CultureInfo.InvariantCulture)
        });
        await db.SaveChangesAsync(ct);
        if (transaction is not null) await transaction.CommitAsync(ct);
        return ServiceResult<ImportResult>.Success(result);
    }
}
