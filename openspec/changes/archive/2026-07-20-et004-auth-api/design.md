## Context

ET003 delivered the auth service layer (`IJwtTokenService`, `IRefreshTokenService`, `IPasswordHasher`,
`IPasswordPolicyValidator`, `IUserRepository`, `IEmployeeRepository`, `IRefreshTokenRepository`,
`IUnitOfWork`, rate-limiter policies, role authorization policies) but zero HTTP endpoints —
`backend/src/Api/Controllers/` contains only `HealthController`. No request/response DTOs,
validators, or global exception handler exist anywhere in the solution yet. This design wires the
four ET004 endpoints on top of ET003's services and fills the specific gaps that block doing so
safely (see Decisions).

Confirmed from reading the actual code (not just docs) before drafting this:
- `Employee` (`Domain/Entities/Employee.cs`): `EmployeeNumber` (string), `IsActive` (bool),
  `FirstName`, `LastName`, `Role` (`EmployeeRole`), nullable `User? User` nav property.
- `User` (`Domain/Entities/User.cs`): `EmployeeId` (Guid FK) has a **unique index**
  (`UserConfiguration.cs:17`) — Employee↔User is strictly 1:1.
- `IRefreshTokenService.RedeemAsync` already handles rotation and reuse-detection (revokes all
  active tokens on reuse) internally — `RefreshTokenService.cs:44-65`.
- `IEmployeeRepository.GetByEmployeeNumberAsync` exists but does not `Include(e => e.User)`.
- No `FluentValidation` package reference exists in any `.csproj` yet.
- No global exception handler / `IExceptionHandler` exists yet — unhandled exceptions currently
  fall through to ASP.NET Core's default behavior, not the standard error envelope.

## Goals / Non-Goals

**Goals:**
- Implement `POST /api/auth/{register,login,refresh,logout}` per `docs/SDS.md` §5.1 and the
  approved spec deltas in `openspec/changes/et004-auth-api/specs/`.
- Reuse ET003's services as-is wherever their existing contract already fits.
- Establish the FluentValidation pattern and the minimal global exception handler that this
  ticket needs and that subsequent tickets (ET005+) will reuse.

**Non-Goals:**
- Forgot-password/OTP (ET005).
- Any change to `jwt-token-issuance`, `password-hashing`, `auth-rate-limiting`, or
  `role-based-authorization` specs — all reused unmodified.
- Rate limiting or new policies for `refresh`/`logout` (explicitly decided against — see proposal).
- Frontend auth module (ET016).

## Decisions

### D1 — New `IAuthService` orchestrates all four flows; controller stays a thin dispatcher
`backend/src/Application/Auth/IAuthService.cs` (new):
```csharp
namespace Application.Auth;

public interface IAuthService
{
    Task<AuthResult> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken);
    Task<AuthResult> LoginAsync(LoginRequest request, CancellationToken cancellationToken);
    Task<AuthResult> RefreshAsync(RefreshRequest request, CancellationToken cancellationToken);
    Task<AuthResult> LogoutAsync(Guid userId, LogoutRequest request, CancellationToken cancellationToken);
}
```
One service (not four) because all four flows share the same `AuthResult`/`AuthFailureReason`
shape and the controller-side error mapping is identical — matches "single responsibility"
(auth orchestration) rather than "single method", consistent with how `RefreshTokenService`
already groups issue/redeem/revoke.

### D2 — Result-object pattern for expected failures, matching ET003's existing convention
`RefreshTokenService.RedeemAsync` already returns a result record
(`RefreshTokenRedemptionResult`) rather than throwing for expected failures (not-found, expired,
reuse). ET004 follows the same convention rather than introducing exceptions for expected
business outcomes:

```csharp
namespace Application.Auth;

public enum AuthFailureReason
{
    None,
    EmployeeNumberInvalid,   // unknown / inactive / already-registered — all identical (spec)
    EmailAlreadyRegistered,  // 409
    PasswordPolicyViolation, // 400, fields: ["password"]
    InvalidCredentials,      // login: 401 generic
    RefreshTokenInvalid,     // refresh/logout: 401 generic
}

public record AuthResult(
    bool Succeeded,
    UserDto? User,
    string? AccessToken,
    string? RefreshToken,
    AuthFailureReason FailureReason)
{
    public static AuthResult Success(UserDto user, string accessToken, string refreshToken) =>
        new(true, user, accessToken, refreshToken, AuthFailureReason.None);

    public static AuthResult Failure(AuthFailureReason reason) =>
        new(false, null, null, null, reason);
}
```
`AuthController` maps `FailureReason` → HTTP status + `ErrorResponse` via a single private
switch, mirroring the existing inline-envelope pattern already used in
`EnvelopeAuthorizationMiddlewareResultHandler` and the rate-limiter `OnRejected` handler — no new
abstraction introduced for this.

**Alternative considered**: throw typed exceptions (`EmployeeNotFoundException`, etc.) caught by a
global handler. Rejected for expected/anticipated outcomes — it would mean two different error
patterns coexisting (`RefreshTokenRedemptionResult` for existing token redemption vs. exceptions
for everything else), which is more inconsistent than extending the result-object pattern already
established.

### D3 — Introduce a minimal global exception handler (new, small scope addition)
`docs/SDS.md` §9.2 and `backend/CLAUDE.md`'s anti-pattern list ("let the global exception handler
do its job") both assume one exists — it doesn't yet. Without it, any *unexpected* exception
(e.g., a transient DB failure during registration) would bypass the standard error envelope
entirely. This ticket adds the minimal version:

- `backend/src/Api/ErrorHandling/GlobalExceptionHandler.cs` — implements .NET 9's
  `IExceptionHandler`, catches anything unhandled, logs
  (timestamp/traceId/exception type+message/stack trace/method/path/UserId-if-available, per
  `docs/SDS.md` §9.3 — never logs passwords/hashes/tokens/OTPs), and writes the standard
  `500 INTERNAL_SERVER_ERROR` envelope with no internal detail leaked to the client.
- Wired via `builder.Services.AddExceptionHandler<GlobalExceptionHandler>()` +
  `builder.Services.AddProblemDetails()` + `app.UseExceptionHandler()` in `Program.cs`.
- This is genuinely shared infrastructure (not auth-specific) but has no other owner in
  `docs/TICKETS.md`, and ET004 is the first ticket whose endpoints can realistically throw.
  Scope is deliberately minimal: catch-all → 500 only. No per-exception-type mapping is added,
  since D2 keeps expected auth failures out of the exception path entirely.

### D4 — Registration's User-create + token-issue wrapped in one explicit DB transaction
Registering both creates a `User` row (via `IUserRepository.AddAsync` + `IUnitOfWork.SaveChangesAsync`)
and issues a refresh token (via `IRefreshTokenService.IssueAsync`, which — per
`RefreshTokenService.cs:38-39` — calls `IUnitOfWork.SaveChangesAsync` itself internally). Called
back-to-back, that's two separate implicit transactions: if the second `SaveChangesAsync` fails
(e.g., transient DB error), the `User` row is already committed but the client receives a 500 with
no tokens — and the employee number is now permanently "already registered" per the
`user-registration` spec's own duplicate-check requirement, with no way to recover without manual
DB intervention.

**Decision**: add `Task ExecuteInTransactionAsync(Func<Task> operation, CancellationToken cancellationToken)`
to `IUnitOfWork` (new method, additive — does not change the existing `SaveChangesAsync` signature).
`AuthService.RegisterAsync` wraps the create-user + issue-token sequence in this call. EF Core's
ambient transaction means the *existing*, unmodified `RefreshTokenService.IssueAsync` still calls
`SaveChangesAsync` internally, and that nested call participates in the same outer transaction —
no change to `IRefreshTokenService`'s contract or its ET003 unit tests.

**Alternatives considered**:
- Remove `IssueAsync`'s internal `SaveChangesAsync` and push it to every caller — rejected: changes
  an already-shipped ET003 contract's behavior for callers/tests that don't yet exist for ET004
  but might already exist for ET003, for no benefit over the transaction-wrapping approach.
- Accept the non-atomic risk as-is — rejected: the "already-registered" check added to the
  `user-registration` spec makes an orphaned User row a permanent dead end for that employee
  number, which is worse than the small cost of adding one `IUnitOfWork` method.

Login/refresh/logout each perform exactly one logical write and need no transaction wrapper.

### D5 — FluentValidation validators invoked explicitly by the controller, not auto-bound
No FluentValidation-ASP.NET-Core auto-validation integration package is added (that ecosystem is
largely unmaintained/deprecated). Instead:
- `RegisterRequestValidator`, `LoginRequestValidator`, `RefreshRequestValidator`,
  `LogoutRequestValidator` : `AbstractValidator<T>` in `Application/Auth/`.
- Registered via `services.AddValidatorsFromAssemblyContaining<RegisterRequestValidator>()`.
- `AuthController` injects `IValidator<T>` per action, calls `ValidateAsync` first, and on failure
  maps `ValidationResult.Errors` (`PropertyName` → `fields`) to `400 VALIDATION_ERROR` before ever
  calling `IAuthService` — matches `docs/SDS.md` §9.4 ("validation executes before business logic")
  and keeps the controller free of business logic (input-shape validation is a boundary concern,
  not a business rule).
- Password complexity itself stays owned by the existing `IPasswordPolicyValidator` (called inside
  `AuthService.RegisterAsync`, not duplicated in the FluentValidation validator) — FluentValidation
  here only checks presence/format (required fields, email format), not the 8-char/letter/digit
  policy, avoiding two sources of truth for the same rule.

### D6 — `EmployeeRepository.GetByEmployeeNumberAsync` gains `.Include(e => e.User)`
Minimal change to the existing (currently unused by any endpoint) method so
`AuthService.RegisterAsync` can check `employee.User is not null` in the same query, without a
second round trip. No interface signature change.

### D7 — New `IRefreshTokenService.RevokeAsync(Guid userId, string rawToken, CancellationToken)`
Added to the existing interface (additive) for the `user-logout` capability and the
`refresh-token-lifecycle` spec's new "Single Refresh Token Revocation" requirement:
```csharp
Task<bool> RevokeAsync(Guid userId, string rawToken, CancellationToken cancellationToken);
```
Returns `false` (revokes nothing) if the token doesn't exist, doesn't belong to `userId`, or is
already inactive; `AuthService.LogoutAsync` maps `false` → `AuthFailureReason.RefreshTokenInvalid`
→ `401`, matching `docs/SDS.md` §5.1 (logout's only documented error is 401).

## API Contracts (DTOs)

All new files in `backend/src/Application/Auth/`:

```csharp
public record RegisterRequest(string EmployeeNumber, string Email, string Password);
public record LoginRequest(string Email, string Password);
public record RefreshRequest(string RefreshToken);
public record LogoutRequest(string RefreshToken);

public record UserDto(Guid Id, string Email, string EmployeeNumber, string FirstName, string LastName, string Role);
public record AuthResponse(UserDto User, string AccessToken, string RefreshToken);
public record RefreshResponse(string AccessToken, string RefreshToken);
```

`AuthController` (`backend/src/Api/Controllers/AuthController.cs`, new):

| Action | Route | Attributes | Success |
|--------|-------|-----------|---------|
| `Register` | `POST /api/auth/register` | `[EnableRateLimiting(AuthRateLimitPolicyNames.Register)]` | `201 Created` → `AuthResponse` |
| `Login` | `POST /api/auth/login` | `[EnableRateLimiting(AuthRateLimitPolicyNames.Login)]` | `200 OK` → `AuthResponse` |
| `Refresh` | `POST /api/auth/refresh` | none (no auth, no rate limit — D-decision in proposal) | `200 OK` → `RefreshResponse` |
| `Logout` | `POST /api/auth/logout` | `[Authorize]` | `204 No Content` |

`Logout` reads the caller's `UserId` from the `sub` claim via a small new extension
`ClaimsPrincipalExtensions.GetUserId()` (`backend/src/Api/Authentication/`), mirroring the pattern
already in `EmployeeRoleResolutionMiddleware.cs:21-22`.

## DB Changes

**None.** No new entities, columns, or migrations. `User`, `Employee`, `RefreshToken` already have
every field ET004 needs (confirmed by reading `docs/SDS.md` §3.2–3.4 and the actual entity files).

## Reuse Summary

| Existing (ET003) | Reused as-is | Modified |
|---|---|---|
| `IJwtTokenService` | ✅ `GenerateAccessToken` | — |
| `IPasswordHasher` | ✅ `Hash`/`Verify` | — |
| `IPasswordPolicyValidator` | ✅ `IsSatisfiedBy` | — |
| `IUserRepository` | ✅ `GetByNormalizedEmailAsync`, `GetByIdWithEmployeeAsync` | — |
| `IEmployeeRepository` | — | ✅ D6: `.Include(e => e.User)` |
| `IRefreshTokenService` | ✅ `IssueAsync`, `RedeemAsync` | ✅ D7: add `RevokeAsync` |
| `IUnitOfWork` | ✅ `SaveChangesAsync` | ✅ D4: add `ExecuteInTransactionAsync` |
| Rate-limit policies | ✅ `AuthRegister`, `AuthLogin` | — (no new policies, per proposal) |

## Checkpoints

```bash
# from backend/
dotnet build                                              # compile + analyzers
dotnet test --filter FullyQualifiedName~UnitTests         # AuthService, validators, GlobalExceptionHandler
dotnet test --filter FullyQualifiedName~IntegrationTests  # AuthController via WebApplicationFactory
```
No frontend changes in this ticket (ET016 consumes these endpoints later) — no frontend
build/lint/test checkpoints apply.

## Risks / Trade-offs

- [Two `SaveChangesAsync` calls remain inside D4's transaction wrapper, just made atomic] →
  Mitigated by the explicit `ExecuteInTransactionAsync` wrapper; still worth a dedicated
  integration test that forces the second save to fail and asserts the `User` row is rolled back.
- [Global exception handler (D3) is broader-scoped than "register/login/refresh/logout"] →
  Deliberately minimal (catch-all → 500 only, no per-type mapping) to avoid scope creep while
  still closing a real gap; future tickets may extend it, not replace it.
- [`RevokeAsync` (D7) and the existing `RevokeAllAsync` now overlap conceptually] → Kept as two
  distinct methods (not one parameterized method) since their callers, semantics, and the two
  separate spec requirements they satisfy ("Single" vs. "Bulk" revocation) are permanently
  distinct — reuse-detection and ET005 password-reset must always mean "all", logout must always
  mean "one".

## Open Questions

None outstanding — the two ambiguities surfaced while drafting this design (registration field
naming, already-registered-employee handling) were resolved with the user and are reflected in
`ADR-0003` and the updated `user-registration` spec delta respectively.
