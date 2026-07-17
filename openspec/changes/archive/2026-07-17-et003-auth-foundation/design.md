## Context

`domain-model` (ET002) already models `User`, `RefreshToken`, `PasswordResetOtp`, and `Employee`
(with `EmployeeRole`), plus `IUserRepository`/`IRefreshTokenRepository`/`IEmployeeRepository` and
an `IUnitOfWork`. No authentication, token issuance, password hashing, or authorization pipeline
exists yet — `Program.cs` only wires EF Core, controllers, and the seed runner; there is no
`UseAuthentication()`, no `[Authorize]` usage, and no NuGet package for JWT or BCrypt. This ticket
adds that foundation as pure `Application`/`Api` infrastructure, per the approved proposal: **no
new controller endpoints** ship in ET003.

## Goals / Non-Goals

**Goals:**
- Issue and validate HS256 JWT access tokens (`sub`-only claim, 15 min).
- Generate, hash, rotate, and bulk-revoke refresh tokens against the existing `RefreshToken` table.
- Hash/verify passwords with BCrypt and validate password complexity.
- Resolve `EmployeeRole` per request from the `Employee` record (never from the JWT) and expose it
  as named, policy-based authorization requirements.
- Register (but not yet consume) rate-limiter policies for the four auth endpoints.
- Prove all of the above end-to-end via tests, without adding a shipped `Api` route.

**Non-Goals:**
- `register`/`login`/`refresh`/`logout` endpoints (ET004).
- `forgot-password`/`reset-password` endpoints, OTP generation/hashing (ET005).
- A global exception handler for arbitrary unhandled exceptions (out of this ticket's scope — only
  the two response paths this ticket introduces, 401 and 429, need a response body; a full
  app-wide handler is deferred to whichever ticket first needs to catch business-logic exceptions).
- Tuning the real numeric rate-limit thresholds (placeholder values only — see Open Questions).

## Decisions

### D1 — Role resolution is real ASP.NET Core middleware, not `IClaimsTransformation`
`IClaimsTransformation` cannot short-circuit the pipeline with a custom 401 body — it can only
add/omit claims, and ASP.NET Core's normal reaction to "no role claim" is a `403` from
authorization, not the `401 AUTHENTICATION_FAILED` the spec requires for a missing/inactive
Employee. A dedicated `IMiddleware` (`EmployeeRoleResolutionMiddleware`), registered after
`UseAuthentication()` and before `UseAuthorization()`, can inspect `HttpContext.User`, load the
`Employee`, and either attach a `ClaimTypes.Role` claim or write the 401 envelope directly and
short-circuit. This matches `docs/SDS.md` §4.6's literal description of a middleware that
"resolves the user, loads Employee, determines role, populates request context."

### D2 — Role policies use built-in `RequireRole`, not a custom `IAuthorizationHandler`
Since D1 already attaches a standard `ClaimTypes.Role` claim, `options.AddPolicy(role,
p => p.RequireRole(role))` for each of the four `EmployeeRole` values is sufficient — no custom
`IAuthorizationRequirement`/`IAuthorizationHandler` needed. Controllers use
`[Authorize(Policy = "Manager")]` etc., never an inline `if (role == ...)` (`backend/CLAUDE.md`).

### D3 — `IJwtTokenService` validates independently of the ASP.NET Core JWT bearer handler
The bearer handler (`AddJwtBearer`) validates tokens for real HTTP requests, but the spec's
"expired token rejected" / "tampered token rejected" scenarios need to be unit-testable without a
running host. `IJwtTokenService.TryValidateAccessToken` wraps `JwtSecurityTokenHandler` directly,
and `Program.cs` configures `TokenValidationParameters` for the bearer handler from the *same*
`JwtOptions`, so both paths agree by construction (one source of configuration, two callers).

### D4 — Password complexity validator is a plain interface, not FluentValidation
`backend/CLAUDE.md` allows either FluentValidation or DataAnnotations "whichever the project
adopts first." ET003 has no request DTOs to validate (no endpoints), so adopting FluentValidation
now would add a NuGet dependency with nothing yet to attach it to. `IPasswordPolicyValidator` is a
plain, directly-unit-testable interface; ET004's registration/reset DTOs can wrap it in whichever
validation framework that ticket adopts.

### D5 — Test-only endpoint lives in a new `IntegrationTests` project via `WebApplicationFactory`
Per the earlier reviewed decision: a `CustomWebApplicationFactory<Program>` maps a throwaway
authenticated endpoint through `ConfigureTestServices`/`IEndpointRouteBuilder`, layered on the real
`Program.cs` pipeline. `Program.cs` needs `public partial class Program;` appended (standard
ASP.NET Core testing requirement — top-level statements otherwise produce an internal, unreferenceable
`Program` type). This fixture is built once here and reused by ET004/ET005.

### D6 — Bulk revoke and role lookup extend existing repository interfaces, not new ones
`domain-model`'s "Per-Aggregate Repository Abstractions" requirement establishes one repository per
aggregate but doesn't enumerate their methods. ET003 adds two methods to already-existing
interfaces rather than introducing new repository types:
- `IUserRepository.GetByIdWithEmployeeAsync(Guid id, CancellationToken)` — loads a `User` with its
  `Employee` navigation included, for role resolution.
- `IRefreshTokenRepository.GetActiveByUserIdAsync(Guid userId, CancellationToken)` — loads all
  unrevoked, unexpired tokens for a user, for bulk revoke and reuse detection.

This is called out explicitly (not a silent change) but is not treated as a `domain-model` spec
delta, since the spec's requirement text doesn't fix method signatures.

### D7 — Error responses reuse one shared envelope DTO, no `ErrorCodes` constants class yet
Both the 401 (role resolution) and 429 (rate limiter) paths need to emit
`{ "error": { "code", "message", "fields", "traceId" } }`. A minimal `Shared.ErrorHandling.ErrorResponse`
/ `ErrorDetail` record pair is added to `backend/src/Shared` (cross-cutting, per `AGENTS.md` §12)
for both to reuse. A dedicated error-code constants class is deferred — only two literal codes
exist right now (`AUTHENTICATION_FAILED`, `RATE_LIMIT_EXCEEDED`); introducing a constants class for
two values is premature and can be added when the full global exception handler is built.

### D8 — Forbidden (403) responses also need the envelope shape
The spec requires `403` + `AUTHORIZATION_FAILED` for a policy mismatch, but ASP.NET Core's default
authorization failure is an empty body. `EnvelopeAuthorizationMiddlewareResultHandler`
(`IAuthorizationMiddlewareResultHandler`) wraps the default handler, only overriding the
`Forbidden` outcome to write the shared envelope; `Success`/`Challenge` outcomes still delegate to
the default handler unchanged.

### D9 — Unit tests use hand-written fakes, not a mocking library
No mocking library (Moq/NSubstitute) is referenced anywhere in the solution today; existing tests
(e.g. `ThrowingEmployeeCsvParser` in `EmployeeCsvSeedRunnerTests`) hand-write small fakes against
the interfaces. `RefreshTokenServiceTests`/role-resolution unit tests follow the same convention —
no new test-only dependency.

## File Paths

**New — `backend/src/Application/Auth/`**
- `IJwtTokenService.cs`, `JwtTokenService.cs`, `JwtOptions.cs`
- `IRefreshTokenService.cs`, `RefreshTokenService.cs`, `RefreshTokenOptions.cs`,
  `RefreshTokenRedemptionResult.cs` (record + `RefreshTokenRedemptionFailureReason` enum)
- `IPasswordHasher.cs`, `BCryptPasswordHasher.cs`
- `IPasswordPolicyValidator.cs`, `PasswordPolicyValidator.cs`

**New — `backend/src/Api/Authentication/`**
- `EmployeeRoleResolutionMiddleware.cs`

**New — `backend/src/Api/Authorization/`**
- `AuthorizationPolicyNames.cs`
- `EnvelopeAuthorizationMiddlewareResultHandler.cs`

**New — `backend/src/Api/RateLimiting/`**
- `AuthRateLimitPolicyNames.cs`
- `AuthRateLimitOptions.cs`

**New — `backend/src/Api/Extensions/`**
- `AuthServiceCollectionExtensions.cs` (bundles JWT bearer + policies + rate limiter + service DI
  registration into one `AddAuthFoundation(IServiceCollection, IConfiguration)` call, keeping
  `Program.cs` thin)

**New — `backend/src/Shared/ErrorHandling/`**
- `ErrorResponse.cs` (`ErrorResponse` + `ErrorDetail` records)

**Modified**
- `backend/src/Domain/Repositories/IUserRepository.cs` — add `GetByIdWithEmployeeAsync`
- `backend/src/Domain/Repositories/IRefreshTokenRepository.cs` — add `GetActiveByUserIdAsync`
- `backend/src/Infrastructure/Persistence/Repositories/UserRepository.cs` — implement it
  (`Include(u => u.Employee)`)
- `backend/src/Infrastructure/Persistence/Repositories/RefreshTokenRepository.cs` — implement it
- `backend/src/Api/Program.cs` — call `AddAuthFoundation`, add `UseRateLimiter()`,
  `UseAuthentication()`, `UseMiddleware<EmployeeRoleResolutionMiddleware>()`, `UseAuthorization()`
  in that order; append `public partial class Program;`
- `backend/src/Api/appsettings.json` — add `Jwt` (Issuer, Audience, AccessTokenLifetimeMinutes —
  **not** SigningKey), `RefreshToken:LifetimeDays`, `RateLimiting:AuthEndpoints` sections
- `backend/src/Api/Api.csproj` — add `Microsoft.AspNetCore.Authentication.JwtBearer`
- `backend/src/Application/Application.csproj` — add `System.IdentityModel.Tokens.Jwt`,
  `BCrypt.Net-Next`

**New project — `backend/tests/IntegrationTests/`**
- `IntegrationTests.csproj` (references `Api`, `Microsoft.AspNetCore.Mvc.Testing`, `xunit`)
- `CustomWebApplicationFactory.cs`
- `JwtAuthenticationTests.cs`, `RoleResolutionMiddlewareTests.cs`,
  `RoleBasedAuthorizationPolicyTests.cs`, `RateLimitingTests.cs`
- `backend/ExpenseTracker.sln` — add the new project

**New — `backend/tests/UnitTests/Application/Auth/`**
- `JwtTokenServiceTests.cs`, `RefreshTokenServiceTests.cs`, `BCryptPasswordHasherTests.cs`,
  `PasswordPolicyValidatorTests.cs`
**New — `backend/tests/UnitTests/Api/`**
- `AuthServiceCollectionExtensionsDiTests.cs` (DI-resolution test, same style as
  `RepositoryDiRegistrationTests`)

## Key Types (matching `docs/SDS.md` §4)

```csharp
// Application/Auth/IJwtTokenService.cs
public interface IJwtTokenService
{
    string GenerateAccessToken(Guid userId);
    bool TryValidateAccessToken(string token, out Guid userId);
}

// Application/Auth/JwtOptions.cs — bound from configuration section "Jwt"; SigningKey via user-secrets
public class JwtOptions
{
    public const string SectionName = "Jwt";
    public string SigningKey { get; set; } = null!;
    public string Issuer { get; set; } = "ExpenseTracker";
    public string Audience { get; set; } = "ExpenseTracker";
    public int AccessTokenLifetimeMinutes { get; set; } = 15;
}

// Application/Auth/IRefreshTokenService.cs
public interface IRefreshTokenService
{
    Task<string> IssueAsync(Guid userId, CancellationToken cancellationToken);
    Task<RefreshTokenRedemptionResult> RedeemAsync(string rawToken, CancellationToken cancellationToken);
    Task RevokeAllAsync(Guid userId, CancellationToken cancellationToken);
}

public record RefreshTokenRedemptionResult(
    bool Succeeded,
    Guid? UserId,
    string? NewRawToken,
    RefreshTokenRedemptionFailureReason? FailureReason);

public enum RefreshTokenRedemptionFailureReason { NotFound, Expired, ReuseDetected }

// Application/Auth/IPasswordHasher.cs
public interface IPasswordHasher
{
    string Hash(string plaintextPassword);
    bool Verify(string plaintextPassword, string hash);
}

// Application/Auth/IPasswordPolicyValidator.cs
public interface IPasswordPolicyValidator
{
    bool IsSatisfiedBy(string password);
}

// Shared/ErrorHandling/ErrorResponse.cs
public record ErrorResponse(ErrorDetail Error);
public record ErrorDetail(string Code, string Message, IReadOnlyList<string> Fields, string TraceId);
```

## DB Changes

**None.** ET003 consumes the existing `User`/`RefreshToken`/`Employee` schema from `domain-model`
as-is; the two new repository methods (D6) are additive query methods, not schema changes — no new
migration is required. Fully backward compatible.

## Reuse

- `IUserRepository`, `IRefreshTokenRepository`, `IEmployeeRepository`, `IUnitOfWork` (ET002) —
  extended, not replaced.
- `ApplicationDbContext`, existing EF configurations — untouched.
- Test conventions: hand-written fakes (D9), `TestDbContextFactory` pattern for any config-level
  test, DI-registration test style from `RepositoryDiRegistrationTests`.
- `dotnet user-secrets` pattern already established for `ConnectionStrings:DefaultConnection` is
  extended to `Jwt:SigningKey`.

## Risks / Trade-offs

- **[Risk]** Rate-limit numeric thresholds are placeholders with no real endpoint to load-test
  against. → **Mitigation**: values are configuration-driven (`appsettings.json`), not hard-coded;
  ET004/ET005 revisit before those endpoints ship; logged as an open ADR per the proposal.
- **[Risk]** `EmployeeRoleResolutionMiddleware` adds one extra DB query per authenticated request.
  → **Mitigation**: matches `docs/AGENTS.md` §7's explicit requirement to never trust a cached/JWT
  role; correctness over micro-optimization. Can be revisited with caching if profiling later shows
  it matters — not a concern for this ticket's scale.
- **[Risk]** `IMiddleware` requires registering `EmployeeRoleResolutionMiddleware` in DI (scoped) in
  addition to `UseMiddleware<T>()` — easy to forget one half. → **Mitigation**: both registrations
  live together in `AuthServiceCollectionExtensions.AddAuthFoundation`, not scattered in
  `Program.cs`.

## Migration Plan

No data migration. Rollout is additive: merging this ticket adds unused-by-clients infrastructure
(no route depends on it yet). Rollback is a plain revert — no schema or data to unwind.

## Open Questions

- Exact numeric rate-limit thresholds (permit limit / window) — placeholder now, needs an ADR once
  ET004/ET005 endpoints exist to test against (already flagged in `proposal.md`).
- Whether the eventual global exception handler (for arbitrary unhandled exceptions) should also
  own the 401/429 envelope-writing this ticket introduces inline, or keep them separate — deferred
  until that handler is actually built.

## Checkpoints

Run from `backend/`, in order, stopping at the first failure:

```bash
dotnet build                                              # compiles all 5 projects + both test projects
dotnet test --filter FullyQualifiedName~UnitTests          # Application/Auth + Api DI tests
dotnet test --filter FullyQualifiedName~IntegrationTests   # JWT/middleware/policy/rate-limit pipeline
dotnet format                                              # formatting/analyzer check
```

No frontend changes in this ticket — `pnpm --filter frontend *` checkpoints are not applicable.
