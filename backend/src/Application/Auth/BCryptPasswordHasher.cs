namespace Application.Auth;

public class BCryptPasswordHasher : IPasswordHasher
{
    public string Hash(string plaintextPassword) => BCrypt.Net.BCrypt.HashPassword(plaintextPassword);

    public bool Verify(string plaintextPassword, string hash) => BCrypt.Net.BCrypt.Verify(plaintextPassword, hash);
}
