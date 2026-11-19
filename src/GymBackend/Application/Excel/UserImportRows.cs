using System.Globalization;
using System.Text;
using GymBackend.Application.Auth;
using GymBackend.Domain;
using GymBackend.Models;

namespace GymBackend.Application.Excel;

public sealed record UserImportRow(int Row, string Rut, string FirstName, string LastName, string Email,
    string Category, string Branch, bool HasFormula = false)
{
    public string Action { get; set; } = "Crear";
    public List<string> Messages { get; set; } = [];
}

public static class UserImportRows
{
    public static string NormalizeHeader(string value) => string.Concat(value.Trim().Normalize(NormalizationForm.FormD)
        .Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)).ToLowerInvariant().Normalize();

    public static List<UserImportRow> Validate(IEnumerable<UserImportRow> input, IReadOnlyList<SportBranch> branches,
        IReadOnlySet<string> existingRuts, IReadOnlySet<string> existingEmails)
    {
        var rows = input.Select(r => r with
        {
            Rut = RutFormat.TryNormalize(r.Rut, out var rut) ? rut : r.Rut.Trim(),
            FirstName = r.FirstName.Trim(),
            LastName = r.LastName.Trim(),
            Email = r.Email.Trim().ToLowerInvariant(),
            Category = r.Category.Trim(),
            Branch = r.Branch.Trim(),
            Messages = [],
            Action = "Crear",
        }).ToList();
        var duplicateRuts = rows.GroupBy(r => r.Rut).Where(g => g.Count() > 1).Select(g => g.Key).ToHashSet();
        var duplicateEmails = rows.GroupBy(r => r.Email).Where(g => g.Count() > 1).Select(g => g.Key).ToHashSet();
        foreach (var row in rows)
        {
            if (row.HasFormula) row.Messages.Add("La fila contiene fórmulas; solo se admite texto.");
            if (!RutFormat.IsValid(row.Rut)) row.Messages.Add(RutFormat.ValidationMessage);
            if (!InputRules.IsSafeName(row.FirstName) || row.FirstName.Length > 80
                || !InputRules.IsSafeName(row.LastName) || row.LastName.Length > 80) row.Messages.Add("Nombre o apellido no válido.");
            if (!InputRules.IsValidEmail(row.Email)) row.Messages.Add("Correo no válido.");
            var branch = branches.SingleOrDefault(b => string.Equals(b.Name, row.Branch, StringComparison.OrdinalIgnoreCase));
            var identityError = IdentityRules.Validate(Roles.Usuario, row.Category,
                row.Branch.Length == 0 ? null : branch?.Id ?? -1, branch?.Enabled == true, "");
            if (identityError is not null) row.Messages.Add(identityError);
            if (duplicateRuts.Contains(row.Rut) || duplicateEmails.Contains(row.Email)) row.Messages.Add("RUT o correo duplicado dentro del archivo.");
            if (row.Messages.Count > 0) row.Action = "Error";
            else if (existingRuts.Contains(row.Rut) || existingEmails.Contains(row.Email))
            {
                row.Action = "Omitir";
                row.Messages.Add("Ya existe una cuenta con este RUT o correo.");
            }
        }
        return rows;
    }
}
