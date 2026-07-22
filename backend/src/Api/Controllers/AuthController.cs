using Api.Authentication;
using Api.RateLimiting;
using Application.Auth;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Shared.ErrorHandling;

namespace Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;
    private readonly IValidator<RegisterRequest> _registerValidator;
    private readonly IValidator<LoginRequest> _loginValidator;
    private readonly IValidator<RefreshRequest> _refreshValidator;
    private readonly IValidator<LogoutRequest> _logoutValidator;
    private readonly IValidator<ForgotPasswordRequest> _forgotPasswordValidator;
    private readonly IValidator<ResetPasswordRequest> _resetPasswordValidator;

    public AuthController(
        IAuthService authService,
        IValidator<RegisterRequest> registerValidator,
        IValidator<LoginRequest> loginValidator,
        IValidator<RefreshRequest> refreshValidator,
        IValidator<LogoutRequest> logoutValidator,
        IValidator<ForgotPasswordRequest> forgotPasswordValidator,
        IValidator<ResetPasswordRequest> resetPasswordValidator)
    {
        _authService = authService;
        _registerValidator = registerValidator;
        _loginValidator = loginValidator;
        _refreshValidator = refreshValidator;
        _logoutValidator = logoutValidator;
        _forgotPasswordValidator = forgotPasswordValidator;
        _resetPasswordValidator = resetPasswordValidator;
    }

    [HttpPost("register")]
    [EnableRateLimiting(AuthRateLimitPolicyNames.Register)]
    public async Task<IActionResult> Register(RegisterRequest request, CancellationToken cancellationToken)
    {
        var validation = await _registerValidator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return ValidationErrorResult(validation);
        }

        var result = await _authService.RegisterAsync(request, cancellationToken);
        if (!result.Succeeded)
        {
            return FailureResult(result.FailureReason);
        }

        return StatusCode(StatusCodes.Status201Created, new AuthResponse(result.User!, result.AccessToken!, result.RefreshToken!));
    }

    [HttpPost("login")]
    [EnableRateLimiting(AuthRateLimitPolicyNames.Login)]
    public async Task<IActionResult> Login(LoginRequest request, CancellationToken cancellationToken)
    {
        var validation = await _loginValidator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return ValidationErrorResult(validation);
        }

        var result = await _authService.LoginAsync(request, cancellationToken);
        if (!result.Succeeded)
        {
            return FailureResult(result.FailureReason);
        }

        return Ok(new AuthResponse(result.User!, result.AccessToken!, result.RefreshToken!));
    }

    [HttpPost("refresh")]
    public async Task<IActionResult> Refresh(RefreshRequest request, CancellationToken cancellationToken)
    {
        var validation = await _refreshValidator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return ValidationErrorResult(validation);
        }

        var result = await _authService.RefreshAsync(request, cancellationToken);
        if (!result.Succeeded)
        {
            return FailureResult(result.FailureReason);
        }

        return Ok(new RefreshResponse(result.AccessToken!, result.RefreshToken!));
    }

    [HttpPost("logout")]
    [Authorize]
    public async Task<IActionResult> Logout(LogoutRequest request, CancellationToken cancellationToken)
    {
        var validation = await _logoutValidator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return ValidationErrorResult(validation);
        }

        var userId = User.GetUserId();
        var result = await _authService.LogoutAsync(userId, request, cancellationToken);
        if (!result.Succeeded)
        {
            return FailureResult(result.FailureReason);
        }

        return NoContent();
    }

    [HttpPost("forgot-password")]
    [EnableRateLimiting(AuthRateLimitPolicyNames.ForgotPassword)]
    public async Task<IActionResult> ForgotPassword(ForgotPasswordRequest request, CancellationToken cancellationToken)
    {
        var validation = await _forgotPasswordValidator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return ValidationErrorResult(validation);
        }

        await _authService.ForgotPasswordAsync(request, cancellationToken);
        return Ok();
    }

    [HttpPost("reset-password")]
    [EnableRateLimiting(AuthRateLimitPolicyNames.ResetPassword)]
    public async Task<IActionResult> ResetPassword(ResetPasswordRequest request, CancellationToken cancellationToken)
    {
        var validation = await _resetPasswordValidator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return ValidationErrorResult(validation);
        }

        var result = await _authService.ResetPasswordAsync(request, cancellationToken);
        if (!result.Succeeded)
        {
            return FailureResult(result.FailureReason);
        }

        return Ok();
    }

    [HttpGet("me")]
    [Authorize]
    public async Task<IActionResult> Me(CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        var userDto = await _authService.GetCurrentUserAsync(userId, cancellationToken);
        if (userDto is null)
        {
            return FailureResult(AuthFailureReason.RefreshTokenInvalid);
        }

        return Ok(userDto);
    }

    private IActionResult ValidationErrorResult(FluentValidation.Results.ValidationResult validation)
    {
        var fields = validation.Errors.Select(e => e.PropertyName).Distinct().ToList();
        return BadRequest(new ErrorResponse(new ErrorDetail(
            "VALIDATION_ERROR",
            "One or more fields are invalid.",
            fields,
            HttpContext.TraceIdentifier)));
    }

    private IActionResult FailureResult(AuthFailureReason reason)
    {
        return reason switch
        {
            AuthFailureReason.EmployeeNumberInvalid => StatusCode(StatusCodes.Status422UnprocessableEntity, new ErrorResponse(new ErrorDetail(
                "BUSINESS_RULE_VIOLATION",
                "Registration could not be completed.",
                [],
                HttpContext.TraceIdentifier))),
            AuthFailureReason.EmailAlreadyRegistered => Conflict(new ErrorResponse(new ErrorDetail(
                "RESOURCE_CONFLICT",
                "This email is already registered.",
                [],
                HttpContext.TraceIdentifier))),
            AuthFailureReason.PasswordPolicyViolation => BadRequest(new ErrorResponse(new ErrorDetail(
                "VALIDATION_ERROR",
                "Password does not meet complexity requirements.",
                ["password"],
                HttpContext.TraceIdentifier))),
            AuthFailureReason.InvalidCredentials => Unauthorized(new ErrorResponse(new ErrorDetail(
                "AUTHENTICATION_FAILED",
                "Invalid email or password.",
                [],
                HttpContext.TraceIdentifier))),
            AuthFailureReason.RefreshTokenInvalid => Unauthorized(new ErrorResponse(new ErrorDetail(
                "AUTHENTICATION_FAILED",
                "Authentication failed.",
                [],
                HttpContext.TraceIdentifier))),
            AuthFailureReason.OtpExpired => StatusCode(StatusCodes.Status410Gone, new ErrorResponse(new ErrorDetail(
                "RESOURCE_EXPIRED",
                "This one-time code has expired.",
                [],
                HttpContext.TraceIdentifier))),
            AuthFailureReason.OtpInvalid => Unauthorized(new ErrorResponse(new ErrorDetail(
                "AUTHENTICATION_FAILED",
                "This one-time code is invalid.",
                [],
                HttpContext.TraceIdentifier))),
            AuthFailureReason.NewPasswordPolicyViolation => BadRequest(new ErrorResponse(new ErrorDetail(
                "VALIDATION_ERROR",
                "Password does not meet complexity requirements.",
                ["newPassword"],
                HttpContext.TraceIdentifier))),
            _ => StatusCode(StatusCodes.Status500InternalServerError),
        };
    }
}
