namespace GymBackend.Application.Excel;

public static class SpreadsheetSafety
{
    public static string Neutralize(string value) => value.Length > 0 && value[0] is '=' or '+' or '-' or '@' or '\t' or '\r'
        ? "'" + value : value;
}
