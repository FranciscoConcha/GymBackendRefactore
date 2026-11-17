using GymBackend.Models;

namespace GymBackend.Application.Auth;

public interface ITokenService
{
    string CreateToken(User user);
}
