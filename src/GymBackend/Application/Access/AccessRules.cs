using GymBackend.Domain;

namespace GymBackend.Application.Access;

public static class AccessRules
{
    public const string Full = "Se alcanzó el aforo máximo de la sala.";
    public const string NoSession = "El usuario no se encuentra registrado en el horario de ingreso.";

    public static bool IsWithin(TimeOnly start, TimeOnly end, TimeOnly now) =>
        now.ToTimeSpan() >= start.ToTimeSpan() - TimeSpan.FromMinutes(15) && now < end;

    public static bool CanUseGroup(string? category, int? branchId, int groupBranchId, int seats, int usedSeats, bool alreadyUsed) =>
        category == UserCategories.Deportista && branchId == groupBranchId && (alreadyUsed || usedSeats < seats);

    public static string? Rejection(bool enabled, bool active, bool inside, bool eligible, int occupancy, int max) =>
        !enabled || !active ? "El usuario no está habilitado." :
        inside ? "El usuario ya se encuentra dentro de la sala." :
        !eligible ? NoSession : occupancy >= max ? Full : null;

    public static bool ValidDocument(string? value) => value is "CarnetIdentidad" or "TNE" or "CredencialUniversitaria";
}
