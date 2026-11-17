using System.Globalization;
using System.Net.Mail;
using System.Text;

namespace GymBackend.Application.Auth;

public static class InputRules
{
    public static bool IsSafeName(string name)
    {
        if (name.Length is < 2 or > 100) return false;

        foreach (var rune in name.EnumerateRunes())
        {
            // Preserve existing names such as the integration fixture's "Test 12345678".
            if (Rune.IsLetterOrDigit(rune) || Rune.GetUnicodeCategory(rune) is
                UnicodeCategory.NonSpacingMark or UnicodeCategory.SpacingCombiningMark or UnicodeCategory.EnclosingMark)
                continue;
            if (rune.Value is ' ' or '\'' or '\u2019' or '-' or '.') continue;
            return false;
        }
        return true;
    }

    // The caller supplies the trimmed, lower-case email used for persistence.
    public static bool IsValidEmail(string email) =>
        email.Length is > 0 and <= 256 &&
        !email.Any(character => char.IsControl(character) || character is '<' or '>') &&
        MailAddress.TryCreate(email, out var address) &&
        string.Equals(address.Address, email, StringComparison.Ordinal);
}
