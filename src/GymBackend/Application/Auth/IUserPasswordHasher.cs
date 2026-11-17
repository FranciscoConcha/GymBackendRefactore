namespace GymBackend.Application.Auth;

public interface IUserPasswordHasher
{
    string Hash(string password);
    bool Verify(string password, string hash);
}
