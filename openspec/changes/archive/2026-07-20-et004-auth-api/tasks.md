Backend-only ticket — no frontend changes (ET016 consumes these endpoints later), so no
`[PARALLEL]` tasks apply here (nothing to split across worktrees). Frontend build/lint/test
checkpoints are N/A for every phase below.

## 1. Foundation — API Contracts & Infrastructure

- [x] 1.1 Add FluentValidation NuGet package to `backend/src/Application` (**requires explicit
      user approval before running `dotnet add package FluentValidation`**, per `CLAUDE.md`'s
      permission model)
- [x] 1.2 Create DTOs in `backend/src/Application/Auth/`: `RegisterRequest`, `LoginRequest`,
      `RefreshRequest`, `LogoutRequest`, `UserDto`, `AuthResponse`, `RefreshResponse`
- [x] 1.3 Create `AuthFailureReason` enum and `AuthResult` record
      (`backend/src/Application/Auth/AuthResult.cs`) with `Success`/`Failure` factories (design D2)
- [x] 1.4 Create `IAuthService` interface (`backend/src/Application/Auth/IAuthService.cs`) —
      `RegisterAsync`, `LoginAsync`, `RefreshAsync`, `LogoutAsync` (design D1)
- [x] 1.5 Add `IUnitOfWork.ExecuteInTransactionAsync(Func<Task>, CancellationToken)` — interface
      method + EF Core implementation (design D4, additive, does not change `SaveChangesAsync`)
- [x] 1.6 Add `IRefreshTokenService.RevokeAsync(Guid userId, string rawToken, CancellationToken)`
      returning `bool` — interface method + implementation in `RefreshTokenService` (design D7)
- [x] 1.7 Modify `EmployeeRepository.GetByEmployeeNumberAsync` implementation to
      `.Include(e => e.User)` (design D6, no interface signature change)
- [x] 1.8 Create `GlobalExceptionHandler` (`backend/src/Api/ErrorHandling/GlobalExceptionHandler.cs`)
      implementing .NET 9 `IExceptionHandler` — catch-all → 500, standard error envelope, logs
      per `docs/SDS.md` §9.3 without leaking passwords/hashes/tokens/OTPs (design D3)
- [x] 1.9 Wire `AddExceptionHandler<GlobalExceptionHandler>()` + `AddProblemDetails()` +
      `app.UseExceptionHandler()` in `Program.cs`

**Checkpoint:** `dotnet build` (from `backend/`) → 0 errors.

## 2. Core Implementation — Auth Service & Validators

- [x] 2.1 Implement `AuthService.RegisterAsync`: employee-number validation (unknown / inactive /
      already-registered → identical `AuthFailureReason.EmployeeNumberInvalid`, checked before any
      write), case-insensitive email uniqueness (`AuthFailureReason.EmailAlreadyRegistered`),
      password complexity via existing `IPasswordPolicyValidator`
      (`AuthFailureReason.PasswordPolicyViolation`), BCrypt hash via `IPasswordHasher`, and wrap the
      User-create + token-issue sequence in `IUnitOfWork.ExecuteInTransactionAsync` (design D4)
- [x] 2.2 Implement `AuthService.LoginAsync`: verify email + password via `IPasswordHasher.Verify`,
      map any mismatch to the single `AuthFailureReason.InvalidCredentials`, issue access + refresh
      token pair on success
- [x] 2.3 Implement `AuthService.RefreshAsync`: delegate to existing
      `IRefreshTokenService.RedeemAsync`, map any `RefreshTokenRedemptionResult` failure (not-found /
      expired / reuse) to `AuthFailureReason.RefreshTokenInvalid`
- [x] 2.4 Implement `AuthService.LogoutAsync`: call `IRefreshTokenService.RevokeAsync(userId,
      rawToken, ct)`, map `false` to `AuthFailureReason.RefreshTokenInvalid`
- [x] 2.5 Create `RegisterRequestValidator`, `LoginRequestValidator`, `RefreshRequestValidator`,
      `LogoutRequestValidator` (`: AbstractValidator<T>`) — presence/format checks only; password
      complexity stays solely owned by `IPasswordPolicyValidator` (design D5, no duplicated rule)
- [x] 2.6 Register `IAuthService`/`AuthService` (Scoped) and
      `services.AddValidatorsFromAssemblyContaining<RegisterRequestValidator>()` in
      `AuthServiceCollectionExtensions.cs`

**Checkpoint:** `dotnet build` (from `backend/`) → 0 errors.

## 3. Integration — Controller & Wiring

- [x] 3.1 Create `ClaimsPrincipalExtensions.GetUserId()`
      (`backend/src/Api/Authentication/ClaimsPrincipalExtensions.cs`), mirroring the `sub`-claim
      read pattern in `EmployeeRoleResolutionMiddleware.cs:21-22`
- [x] 3.2 Create `AuthController` (`backend/src/Api/Controllers/AuthController.cs`) with
      `Register` (`[EnableRateLimiting(AuthRateLimitPolicyNames.Register)]` → 201),
      `Login` (`[EnableRateLimiting(AuthRateLimitPolicyNames.Login)]` → 200),
      `Refresh` (no auth, no rate limit → 200), `Logout` (`[Authorize]` → 204)
- [x] 3.3 Wire controller-side `IValidator<T>.ValidateAsync` calls for each action, mapping
      `ValidationResult.Errors` to `400 VALIDATION_ERROR` (`fields` populated) before calling
      `IAuthService` (design D5)
- [x] 3.4 Implement the controller-side `AuthFailureReason` → HTTP status + `ErrorResponse`
      mapping switch (design D2) — `EmployeeNumberInvalid`/`EmailAlreadyRegistered` → 409 or 400 per
      spec, `PasswordPolicyViolation` → 400, `InvalidCredentials`/`RefreshTokenInvalid` → 401
- [x] 3.5 Confirm Swagger/OpenAPI picks up all 4 new endpoints with correct request/response
      schemas

**Checkpoint:** `dotnet build` → 0 errors; `dotnet format --verify-no-changes` (from `backend/`).

## 4. Tests (one per spec delta scenario)

### user-registration
- [x] 4.1 Test: unknown employee number is rejected with the generic error
- [x] 4.2 Test: inactive employee's number is rejected with the identical generic error
- [x] 4.3 Test: already-registered employee number is rejected before any database write
- [x] 4.4 Test: active, not-yet-registered employee's number allows registration to proceed
- [x] 4.5 Test: duplicate case-insensitive email is rejected with 409
- [x] 4.6 Test: unique email passes the uniqueness check
- [x] 4.7 Test: non-compliant password is rejected with 400 VALIDATION_ERROR on the `password` field
- [x] 4.8 Test: successful registration returns 201 with tokens and a User object containing no
      password/hash
- [x] 4.9 Test: successful registration persists `User.PasswordHash` as a BCrypt hash only, never
      the plaintext

### user-login
- [x] 4.10 Test: correct credentials issue a token pair with 200 OK
- [x] 4.11 Test: unknown email returns the generic 401 AUTHENTICATION_FAILED
- [x] 4.12 Test: wrong password returns the identical generic 401 AUTHENTICATION_FAILED
- [x] 4.13 Test: missing email or password returns 400 VALIDATION_ERROR identifying the field(s)
- [x] 4.14 Test: successful login persists a refresh token redeemable exactly once against
      `/api/auth/refresh`

### session-refresh
- [x] 4.15 Test: valid, unexpired, unrevoked refresh token returns a new rotated token pair (200)
- [x] 4.16 Test: not-found/expired/reused refresh token returns 401 without distinguishing which
- [x] 4.17 Test: refresh succeeds with no `Authorization` header present

### user-logout
- [x] 4.18 Test: missing/invalid access token is rejected with 401, no token revoked
- [x] 4.19 Test: logout revokes only the supplied token; a second active token for the same user
      remains valid and redeemable
- [x] 4.20 Test: a token revoked via logout cannot be redeemed by `/api/auth/refresh` (401)
- [x] 4.21 Test: supplying a refresh token belonging to a different user is rejected, no token
      revoked
- [x] 4.22 Test: successful logout returns 204 No Content with an empty body

### refresh-token-lifecycle (modified)
- [x] 4.23 Test: single-token revocation sets only that token's `RevokedAt`, leaving the user's
      other active tokens unchanged
- [x] 4.24 Test: single-token revocation for a token belonging to a different user fails and
      modifies no `RefreshToken` row

### Transactional integrity (design D4 risk)
- [x] 4.25 Test: forcing the token-issue step's `SaveChangesAsync` to fail during registration
      rolls back the `User` row (no orphaned, permanently-blocked employee number)

**Checkpoint:** `dotnet test --filter FullyQualifiedName~UnitTests` → all green; `dotnet test
--filter FullyQualifiedName~IntegrationTests` → all green (from `backend/`).

## 5. Archive

- [x] 5.1 Re-run the full quality gate in order: `dotnet build` → `dotnet format
      --verify-no-changes` → unit tests → integration tests — stop and fix at first failure
- [x] 5.2 Run `openspec archive et004-auth-api`
- [x] 5.3 Update `docs/TICKETS.md`: ET004 `Status` column per the ticket's actual state after
      archive (per the Notes convention — archive happening does not itself mean `Done`)
- [x] 5.4 Flag (not perform, unless separately requested) the follow-up docs change noted in
      `ADR-0003`: `docs/SDS.md` §5.1 should say `employeeNumber` instead of `employeeId`
