namespace Application.Auth;

public interface IPasswordHasher
{
    string Hash(string plaintextPassword);
    bool Verify(string plaintextPassword, string hash);
}
