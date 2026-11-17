using System.Text.RegularExpressions;

namespace GymBackend.Application.Auth;

public static class RutFormat
{
    public static bool IsValid(string rut) => Regex.IsMatch(rut, @"^\d{8,9}$");
}
