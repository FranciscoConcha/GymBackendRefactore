using System.Net;
using System.Net.Http.Json;
using ClosedXML.Excel;
using GymBackend.Application.Excel;
using GymBackend.Domain;
using GymBackend.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using static GymBackend.IntegrationTests.Infrastructure.Api;

namespace GymBackend.IntegrationTests.Reporting;

public sealed class ExcelTests(GymApiFactory factory)
{
    [Fact]
    public async Task ConfirmationRevalidatesExistingAccounts_AndConcurrentConfirmationsDoNotDuplicate()
    {
        using var admin = await factory.CreateAdminClientAsync();
        var rut = TestData.NextRut();
        var email = $"race-{Guid.NewGuid():N}@example.test";
        using var upload = Upload(Workbook(rut, email));
        var preview = await Expect(await admin.PostAsync("/api/v1/admin/user-imports", upload, Ct), HttpStatusCode.OK);
        await factory.WithDbAsync(async db =>
        {
            db.Users.Add(new GymBackend.Models.User
            {
                Rut = rut,
                Email = email,
                FirstName = "Ana",
                LastName = "Pérez",
                Category = "Estudiante",
                AccountStatus = AccountStatus.Pendiente,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow,
            });
            await db.SaveChangesAsync(Ct);
        });
        var path = $"/api/v1/admin/user-imports/{preview.GetProperty("id").GetInt32()}/confirm";
        var responses = await Task.WhenAll(admin.PostAsync(path, null, Ct), admin.PostAsync(path, null, Ct));
        foreach (var response in responses)
        {
            var result = await Expect(response, HttpStatusCode.OK);
            Assert.Equal(0, result.GetProperty("created").GetInt32());
            Assert.Equal(1, result.GetProperty("skipped").GetInt32());
        }
        Assert.Equal(1, await factory.WithDbAsync(db => db.Users.CountAsync(u => u.Rut == rut, Ct)));
        Assert.Equal(0, await factory.WithDbAsync(db => db.EmailOutbox.CountAsync(e => e.To == email, Ct)));
    }

    [Fact]
    public async Task PreviewHasNoEffects_ConfirmCreatesPendingAccountsAndOutbox_RepeatHasNoDuplicates()
    {
        using var admin = await factory.CreateAdminClientAsync();
        var rut = TestData.NextRut();
        var email = $"import-{Guid.NewGuid():N}@example.test";
        var outboxBefore = await factory.WithDbAsync(db => db.EmailOutbox.CountAsync(e => e.To == email, Ct));
        using var content = Upload(Workbook(rut, email));
        var preview = await Expect(await admin.PostAsync("/api/v1/admin/user-imports", content, Ct), HttpStatusCode.OK);
        Assert.Equal(1, preview.GetProperty("summary").GetProperty("create").GetInt32());
        Assert.False(await factory.WithDbAsync(db => db.Users.AnyAsync(u => u.Rut == rut || u.Email == email, Ct)));
        Assert.Equal(outboxBefore, await factory.WithDbAsync(db => db.EmailOutbox.CountAsync(e => e.To == email, Ct)));
        var path = $"/api/v1/admin/user-imports/{preview.GetProperty("id").GetInt32()}/confirm";
        var key = Guid.NewGuid().ToString();
        async Task<System.Text.Json.JsonElement> Confirm(string? idempotencyKey)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, path);
            if (idempotencyKey is not null) request.Headers.Add("Idempotency-Key", idempotencyKey);
            return await Expect(await admin.SendAsync(request, Ct), HttpStatusCode.OK);
        }
        var first = await Confirm(key);
        Assert.Equal(1, first.GetProperty("created").GetInt32());
        Assert.Equal(first.GetRawText(), (await Confirm(key)).GetRawText());
        Assert.Equal(first.GetRawText(), (await Confirm(null)).GetRawText());
        await factory.WithDbAsync(async db =>
        {
            var user = await db.Users.SingleAsync(u => u.Email == email, Ct);
            Assert.Equal(AccountStatus.Pendiente, user.AccountStatus);
            Assert.Equal(Roles.Usuario, user.Role);
            Assert.Empty(user.PasswordHash);
            Assert.Single(await db.AccountTokens.Where(t => t.UserId == user.Id).ToArrayAsync(Ct));
            Assert.Single(await db.EmailOutbox.Where(e => e.To == email).ToArrayAsync(Ct));
            Assert.Single(await db.AuditEvents.Where(a => a.Action == "import.confirmed" && a.TargetId == preview.GetProperty("id").GetInt32().ToString()).ToArrayAsync(Ct));
        });
    }

    [Fact]
    public async Task DuplicateRowsAndFormulasAreControlledErrors_AndExistingRowsAreSkipped()
    {
        using var admin = await factory.CreateAdminClientAsync();
        var rut = TestData.NextRut();
        using var duplicate = Upload(Workbook(rut, "duplicate@example.test", duplicate: true));
        var preview = await Expect(await admin.PostAsync("/api/v1/admin/user-imports", duplicate, Ct), HttpStatusCode.OK);
        Assert.Equal(2, preview.GetProperty("summary").GetProperty("error").GetInt32());
        using var formula = Upload(Workbook(TestData.NextRut(), "formula@example.test", formula: true));
        preview = await Expect(await admin.PostAsync("/api/v1/admin/user-imports", formula, Ct), HttpStatusCode.OK);
        Assert.Equal("Error", preview.GetProperty("rows")[0].GetProperty("action").GetString());
        Assert.StartsWith("'=", preview.GetProperty("rows")[0].GetProperty("firstName").GetString());
        using var existing = Upload(Workbook(factory.AdminRut, "existing@example.test"));
        preview = await Expect(await admin.PostAsync("/api/v1/admin/user-imports", existing, Ct), HttpStatusCode.OK);
        Assert.Equal(1, preview.GetProperty("summary").GetProperty("skip").GetInt32());
    }

    [Fact]
    public async Task InvalidOrOversizedFilesAndTooManyRowsAreRejected_ExpiredPreviewConflicts()
    {
        using var admin = await factory.CreateAdminClientAsync();
        foreach (var bytes in new[] { new byte[] { 1, 2, 3 }, new byte[UserImportService.MaxBytes + 1] })
        {
            using var upload = Upload(bytes);
            await Expect(await admin.PostAsync("/api/v1/admin/user-imports", upload, Ct), HttpStatusCode.BadRequest);
        }
        using (var stream = new MemoryStream())
        {
            using var workbook = new XLWorkbook();
            var sheet = workbook.AddWorksheet("Usuarios");
            sheet.Cell(1002, 1).Value = "exceso";
            workbook.SaveAs(stream);
            using var upload = Upload(stream.ToArray());
            await Expect(await admin.PostAsync("/api/v1/admin/user-imports", upload, Ct), HttpStatusCode.BadRequest);
        }
        using var valid = Upload(Workbook(TestData.NextRut(), $"expire-{Guid.NewGuid():N}@example.test"));
        var preview = await Expect(await admin.PostAsync("/api/v1/admin/user-imports", valid, Ct), HttpStatusCode.OK);
        var id = preview.GetProperty("id").GetInt32();
        await factory.WithDbAsync(db => db.UserImports.Where(i => i.Id == id)
            .ExecuteUpdateAsync(s => s.SetProperty(i => i.ExpiresAt, DateTimeOffset.UtcNow.AddHours(-2)), Ct));
        await Expect(await admin.PostAsync($"/api/v1/admin/user-imports/{id}/confirm", null, Ct), HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task ExportHasAllSheets_NoSecrets_NeutralizedText_AndRestrictsReceptionist()
    {
        using var admin = await factory.CreateAdminClientAsync();
        var user = await factory.CreateClientUserAsync();
        using var client = user.Client;
        var secret = "DO_NOT_EXPORT_" + Guid.NewGuid().ToString("N");
        await factory.WithDbAsync(async db =>
        {
            var stored = await db.Users.SingleAsync(u => u.Id == user.UserId, Ct);
            stored.FirstName = "=HYPERLINK(\"https://example.test\")";
            stored.PasswordHash = secret;
            await db.SaveChangesAsync(Ct);
        });
        using var response = await admin.PostAsJsonAsync("/api/v1/admin/exports", new { from = "2026-11-01", to = "2026-11-02", category = "Estudiante" }, JsonOptions, Ct);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("attachment", response.Content.Headers.ContentDisposition?.DispositionType);
        await using var stream = await response.Content.ReadAsStreamAsync(Ct);
        using var workbook = new XLWorkbook(stream);
        Assert.Equal(new[] { "Filtros", "Usuarios", "Sesiones", "Reservas", "Grupos", "Visitas", "Inasistencias" }, workbook.Worksheets.Select(s => s.Name));
        Assert.Contains("America/Santiago", workbook.Worksheet("Filtros").CellsUsed().Select(c => c.GetString()));
        var cells = workbook.Worksheets.SelectMany(s => s.CellsUsed()).ToArray();
        Assert.DoesNotContain(cells, c => c.GetString().Contains(secret, StringComparison.Ordinal));
        Assert.DoesNotContain(cells, c => c.HasFormula);
        Assert.Contains(cells, c => c.GetString().StartsWith("'=HYPERLINK", StringComparison.Ordinal)
            || c.GetString().StartsWith("=HYPERLINK", StringComparison.Ordinal) && c.Style.IncludeQuotePrefix);
        Assert.All(workbook.Worksheet("Usuarios").RowsUsed().Skip(1), r => Assert.Equal("Estudiante", r.Cell(6).GetString()));
        var receptionist = await factory.CreateReceptionistClientAsync();
        using var reception = receptionist.Client;
        using var denied = await reception.PostAsJsonAsync("/api/v1/admin/exports", new { }, JsonOptions, Ct);
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        using var deniedImport = await reception.PostAsync("/api/v1/admin/user-imports/1/confirm", null, Ct);
        Assert.Equal(HttpStatusCode.Forbidden, deniedImport.StatusCode);
    }

    internal static byte[] Workbook(string rut, string email, bool duplicate = false, bool formula = false)
    {
        using var stream = new MemoryStream();
        using var workbook = new XLWorkbook();
        var sheet = workbook.AddWorksheet("Usuarios");
        string[] headers = ["rut", "NOMBRE", "Apellido", "Correo", "Categoría", "Rama"];
        for (var column = 1; column <= headers.Length; column++) sheet.Cell(1, column).Value = headers[column - 1];
        for (var row = 2; row <= (duplicate ? 3 : 2); row++)
        {
            sheet.Cell(row, 1).Value = rut;
            sheet.Cell(row, 2).Value = "Ana";
            if (formula) sheet.Cell(row, 2).FormulaA1 = "1+1";
            sheet.Cell(row, 3).Value = "Pérez";
            sheet.Cell(row, 4).Value = email;
            sheet.Cell(row, 5).Value = "Estudiante";
        }
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    internal static MultipartFormDataContent Upload(byte[] bytes)
    {
        var form = new MultipartFormDataContent();
        form.Add(new ByteArrayContent(bytes), "file", "usuarios.xlsx");
        return form;
    }
}
