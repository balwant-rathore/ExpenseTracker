## 1. Phase 1 — Foundation

- [x] 1.1 Add NuGet dependencies: `Microsoft.AspNetCore.Authentication.JwtBearer` to
      `backend/src/Api/Api.csproj`; `System.IdentityModel.Tokens.Jwt` and `BCrypt.Net-Next` to
      `backend/src/Application/Application.csproj` (requires user approval per `CLAUDE.md` before
      running `dotnet add package`)
- [x] 1.2 Add `Jwt` (Issuer, Audience, AccessTokenLifetimeMinutes), `RefreshToken:LifetimeDays`, and
      `RateLimiting:AuthEndpoints` config sections to `backend/src/Api/appsettings.json` /
      `appsettings.Development.json` — no signing key committed
- [x] 1.3 Set `Jwt:SigningKey` via `dotnet user-secrets set` for local development (per
      `backend/CLAUDE.md` connection-string pattern)
- [x] 1.4 Add `Shared/ErrorHandling/ErrorResponse.cs` (`ErrorResponse` + `ErrorDetail` records) to
      `backend/src/Shared`
- [x] 1.5 Add `IUserRepository.GetByIdWithEmployeeAsync(Guid, CancellationToken)` and implement in
      `UserRepository` (`Include(u => u.Employee)`)
- [x] 1.6 Add `IRefreshTokenRepository.GetActiveByUserIdAsync(Guid, CancellationToken)` and
      implement in `RefreshTokenRepository`
- [x] 1.7 Create `backend/tests/IntegrationTests` project (xUnit + `Microsoft.AspNetCore.Mvc.Testing`,
      project reference to `Api`) and add it to `backend/ExpenseTracker.sln`
- [x] 1.8 Append `public partial class Program;` to `backend/src/Api/Program.cs` so
      `WebApplicationFactory<Program>` can reference it from `IntegrationTests`
- [x] 1.9 **Checkpoint:** `dotnet build` from `backend/` → 0 errors

## 2. Phase 2 — Core Implementation

- [x] 2.1 Implement `JwtOptions` and `IJwtTokenService`/`JwtTokenService`
      (`GenerateAccessToken(Guid)`, `TryValidateAccessToken(string, out Guid)`) in
      `Application/Auth/`
- [x] 2.2 Implement `RefreshTokenOptions`, `RefreshTokenRedemptionResult`,
      `RefreshTokenRedemptionFailureReason`, and `IRefreshTokenService`/`RefreshTokenService`
      (`IssueAsync`, `RedeemAsync`, `RevokeAllAsync`) in `Application/Auth/`
- [x] 2.3 Implement `IPasswordHasher`/`BCryptPasswordHasher` in `Application/Auth/`
- [x] 2.4 Implement `IPasswordPolicyValidator`/`PasswordPolicyValidator` (min 8 chars, ≥1 letter,
      ≥1 digit) in `Application/Auth/`
- [x] 2.5 Implement `AuthorizationPolicyNames` and per-`EmployeeRole` policy registration
      (`RequireRole`) in `Api/Authorization/`
- [x] 2.6 Implement `EmployeeRoleResolutionMiddleware` (`IMiddleware`) in `Api/Authentication/`:
      resolves `Employee` via `sub`, attaches `ClaimTypes.Role`, or writes the 401
      `AUTHENTICATION_FAILED` envelope and short-circuits
- [x] 2.7 Implement `EnvelopeAuthorizationMiddlewareResultHandler`
      (`IAuthorizationMiddlewareResultHandler`) in `Api/Authorization/` — writes the 403
      `AUTHORIZATION_FAILED` envelope on `Forbidden`, delegates otherwise
- [x] 2.8 Implement `AuthRateLimitPolicyNames`, `AuthRateLimitOptions`, and sliding-window
      `RateLimiter` policy registration (one per auth endpoint name) with an `OnRejected` handler
      writing the 429 `RATE_LIMIT_EXCEEDED` envelope, in `Api/RateLimiting/`
- [x] 2.9 Implement `AuthServiceCollectionExtensions.AddAuthFoundation(IServiceCollection,
      IConfiguration)` in `Api/Extensions/` bundling JWT bearer auth, role policies, rate limiter,
      and all `Application`-layer service registrations
- [x] 2.10 **Checkpoint:** `dotnet build` from `backend/` → 0 errors

## 3. Phase 3 — Integration

- [x] 3.1 Call `AddAuthFoundation` from `Program.cs`; wire pipeline order
      `UseRateLimiter()` → `UseAuthentication()` → `UseMiddleware<EmployeeRoleResolutionMiddleware>()`
      → `UseAuthorization()` before `MapControllers()`
- [x] 3.2 Build `CustomWebApplicationFactory<Program>` in `IntegrationTests`, mapping a throwaway
      authenticated test endpoint (plus per-role-policy and per-rate-limit-policy variants) via
      `ConfigureTestServices`/endpoint routing, layered on the real `Program.cs` pipeline
- [x] 3.3 Confirm existing EF Core migrations still apply cleanly with no new migration required
      (`dotnet ef database update` against local dev DB — no schema change expected)
- [x] 3.4 **Checkpoint:** `dotnet build` → 0 errors; `dotnet format --verify-no-changes` → clean

## 4. Phase 4 — Tests (one per spec scenario)

### 4.1 `jwt-token-issuance`
- [x] 4.1.1 Test: issued token's payload contains only the `sub` claim, no `role`/`email`/other claims
- [x] 4.1.2 Test: issued token's `exp` equals issuance time + 15 minutes
- [x] 4.1.3 Test: validation fails for a token presented after `exp` has passed
- [x] 4.1.4 Test: validation fails for a token with an altered payload/signature
- [x] 4.1.5 Test: no committed `appsettings.*.json` file contains a literal JWT signing key value

### 4.2 `refresh-token-lifecycle`
- [x] 4.2.1 Test: issuing a refresh token persists only its SHA-256 hash, never the raw value
- [x] 4.2.2 Test: issued refresh token's `ExpiresAt` equals issuance time + 7 days
- [x] 4.2.3 Test: redeeming a valid refresh token issues a new one and sets the old one's `RevokedAt`
- [x] 4.2.4 Test: redeeming an already-revoked token fails
- [x] 4.2.5 Test: reusing a revoked token revokes every other active token for that user
- [x] 4.2.6 Test: bulk revocation sets `RevokedAt` on every active token for a user and leaves
      already-revoked/expired rows unchanged

### 4.3 `password-hashing`
- [x] 4.3.1 Test: hash-then-verify roundtrip with the same plaintext succeeds
- [x] 4.3.2 Test: verify fails when plaintext doesn't match the hash's original password
- [x] 4.3.3 Test: password shorter than 8 characters is rejected by the complexity validator
- [x] 4.3.4 Test: password with no digit is rejected by the complexity validator
- [x] 4.3.5 Test: password with no letter is rejected by the complexity validator
- [x] 4.3.6 Test: a compliant password (≥8 chars, ≥1 letter, ≥1 digit) is accepted

### 4.4 `role-based-authorization`
- [x] 4.4.1 Test: resolved role reflects an `Employee.Role` change between two requests using the
      same access token (no caching/JWT-embedded role)
- [x] 4.4.2 Test: middleware attaches a `ClaimTypes.Role` claim matching the caller's `Employee.Role`
- [x] 4.4.3 Test: a `User` with no linked `Employee` record is rejected with 401
      `AUTHENTICATION_FAILED`
- [x] 4.4.4 Test: a `User` whose linked `Employee.IsActive` is `false` is rejected with 401
      `AUTHENTICATION_FAILED`
- [x] 4.4.5 Test: a request whose resolved role satisfies a role policy is authorized to proceed
- [x] 4.4.6 Test: a request whose resolved role does not satisfy a role policy is rejected with 403
      `AUTHORIZATION_FAILED`

### 4.5 `auth-rate-limiting`
- [x] 4.5.1 Test: requests at or below the configured sliding-window limit are permitted
- [x] 4.5.2 Test: a request exceeding the configured limit is rejected with 429
      `RATE_LIMIT_EXCEEDED`
- [x] 4.5.3 Test: the 429 response body is identical regardless of whether the request's
      credentials would otherwise have been valid

- [x] 4.6 **Checkpoint:** `dotnet test` (unit) → all green; `dotnet test` (integration) → all green

## 5. Phase 5 — Archive

- [x] 5.1 Run full quality gate in order: `dotnet build` → `dotnet format --verify-no-changes` →
      `dotnet test --filter FullyQualifiedName~UnitTests` →
      `dotnet test --filter FullyQualifiedName~IntegrationTests` (E2E not applicable — no
      user-facing flow in this ticket)
- [x] 5.2 Run `openspec archive et003-auth-foundation`
- [x] 5.3 Update `docs/TICKETS.md` ET003 row status (`In progress` → `PR open (#N)` once the PR is
      opened, per the Status column convention — archiving here does not mean `Done`)

**Note:** This ticket is backend-only infrastructure with no frontend counterpart — no `[PARALLEL]`
frontend/backend split applies; all phases are sequential.
