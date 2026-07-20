using Domain.Entities;
using Domain.Repositories;

namespace Application.Auth;

public class AuthService : IAuthService
{
    private readonly IEmployeeRepository _employeeRepository;
    private readonly IUserRepository _userRepository;
    private readonly IRefreshTokenService _refreshTokenService;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IPasswordPolicyValidator _passwordPolicyValidator;
    private readonly IJwtTokenService _jwtTokenService;
    private readonly IUnitOfWork _unitOfWork;

    public AuthService(
        IEmployeeRepository employeeRepository,
        IUserRepository userRepository,
        IRefreshTokenService refreshTokenService,
        IPasswordHasher passwordHasher,
        IPasswordPolicyValidator passwordPolicyValidator,
        IJwtTokenService jwtTokenService,
        IUnitOfWork unitOfWork)
    {
        _employeeRepository = employeeRepository;
        _userRepository = userRepository;
        _refreshTokenService = refreshTokenService;
        _passwordHasher = passwordHasher;
        _passwordPolicyValidator = passwordPolicyValidator;
        _jwtTokenService = jwtTokenService;
        _unitOfWork = unitOfWork;
    }

    public async Task<AuthResult> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken)
    {
        var employee = await _employeeRepository.GetByEmployeeNumberAsync(request.EmployeeNumber, cancellationToken);
        if (employee is null || !employee.IsActive || employee.User is not null)
        {
            return AuthResult.Failure(AuthFailureReason.EmployeeNumberInvalid);
        }

        var normalizedEmail = request.Email.ToUpperInvariant();
        var existingUser = await _userRepository.GetByNormalizedEmailAsync(normalizedEmail, cancellationToken);
        if (existingUser is not null)
        {
            return AuthResult.Failure(AuthFailureReason.EmailAlreadyRegistered);
        }

        if (!_passwordPolicyValidator.IsSatisfiedBy(request.Password))
        {
            return AuthResult.Failure(AuthFailureReason.PasswordPolicyViolation);
        }

        var now = DateTime.UtcNow;
        var user = new User
        {
            Id = Guid.NewGuid(),
            EmployeeId = employee.EmployeeId,
            Email = request.Email,
            NormalizedEmail = normalizedEmail,
            PasswordHash = _passwordHasher.Hash(request.Password),
            CreatedAt = now,
            UpdatedAt = now,
        };

        string? refreshToken = null;
        await _unitOfWork.ExecuteInTransactionAsync(async () =>
        {
            await _userRepository.AddAsync(user, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            refreshToken = await _refreshTokenService.IssueAsync(user.Id, cancellationToken);
        }, cancellationToken);

        var accessToken = _jwtTokenService.GenerateAccessToken(user.Id);
        var userDto = new UserDto(user.Id, user.Email, employee.EmployeeNumber, employee.FirstName, employee.LastName, employee.Role.ToString());

        return AuthResult.Success(userDto, accessToken, refreshToken!);
    }

    public async Task<AuthResult> LoginAsync(LoginRequest request, CancellationToken cancellationToken)
    {
        var normalizedEmail = request.Email.ToUpperInvariant();
        var user = await _userRepository.GetByNormalizedEmailAsync(normalizedEmail, cancellationToken);

        if (user is null || !_passwordHasher.Verify(request.Password, user.PasswordHash))
        {
            return AuthResult.Failure(AuthFailureReason.InvalidCredentials);
        }

        var accessToken = _jwtTokenService.GenerateAccessToken(user.Id);
        var refreshToken = await _refreshTokenService.IssueAsync(user.Id, cancellationToken);
        var userDto = new UserDto(
            user.Id, user.Email, user.Employee.EmployeeNumber, user.Employee.FirstName, user.Employee.LastName, user.Employee.Role.ToString());

        return AuthResult.Success(userDto, accessToken, refreshToken);
    }

    public async Task<AuthResult> RefreshAsync(RefreshRequest request, CancellationToken cancellationToken)
    {
        var result = await _refreshTokenService.RedeemAsync(request.RefreshToken, cancellationToken);
        if (!result.Succeeded)
        {
            return AuthResult.Failure(AuthFailureReason.RefreshTokenInvalid);
        }

        var accessToken = _jwtTokenService.GenerateAccessToken(result.UserId!.Value);
        return new AuthResult(true, null, accessToken, result.NewRawToken, AuthFailureReason.None);
    }

    public async Task<AuthResult> LogoutAsync(Guid userId, LogoutRequest request, CancellationToken cancellationToken)
    {
        var revoked = await _refreshTokenService.RevokeAsync(userId, request.RefreshToken, cancellationToken);
        return revoked
            ? new AuthResult(true, null, null, null, AuthFailureReason.None)
            : AuthResult.Failure(AuthFailureReason.RefreshTokenInvalid);
    }
}
