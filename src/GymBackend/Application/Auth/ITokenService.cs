using GymBackend.Models;

namespace GymBackend.Application.Auth;

public interface ITokenService
{
    IssuedToken CreateToken(User user);
}
