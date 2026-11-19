using GymBackend.Domain;
namespace GymBackend.Application.Auth;

public static class IdentityRules
{
    public static string? Validate(string role, string? category, int? branchId, bool branchEnabled, string username)
    {
        if (role is not (Roles.Admin or Roles.Recepcionista or Roles.Usuario))
            return "El rol no es v\u00e1lido.";
        if (role != Roles.Usuario)
        {
            if (category is not null || branchId is not null)
                return "El personal no admite categor\u00eda ni rama.";
            return string.IsNullOrWhiteSpace(username) || username.Length > 64
                ? "El personal requiere un usuario de hasta 64 caracteres." : null;
        }
        if (category is not (UserCategories.Deportista or UserCategories.Estudiante or UserCategories.Funcionario or UserCategories.Externo))
            return "La categor\u00eda es obligatoria y debe ser Deportista, Estudiante, Funcionario o Externo.";
        if (category == UserCategories.Deportista)
            return branchId is null || !branchEnabled ? "El deportista requiere una rama habilitada." : null;
        return branchId is not null ? "Solo los deportistas pueden tener una rama." : null;
    }
}
