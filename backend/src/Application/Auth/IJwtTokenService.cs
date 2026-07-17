namespace Application.Auth;

public interface IJwtTokenService
{
    string GenerateAccessToken(Guid userId);
    bool TryValidateAccessToken(string token, out Guid userId);
}
