namespace Application.Auth;

public record AuthResponse(UserDto User, string AccessToken, string RefreshToken);
