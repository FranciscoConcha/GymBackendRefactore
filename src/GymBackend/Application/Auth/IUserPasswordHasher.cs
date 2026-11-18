namespace GymBackend.Application.Auth;

public interface IUserPasswordHasher
{
    string Hash(string password);
    PasswordCheck Verify(string password, string hash);
    void VerifyDummy(string password);
}

public enum PasswordCheck { Failed, Success, SuccessRehashNeeded }
