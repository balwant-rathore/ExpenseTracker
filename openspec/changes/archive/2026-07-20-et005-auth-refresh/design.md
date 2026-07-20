## Context

ET004 (`et004-auth-api`, merged) delivered `AuthController` with `register`/`login`/`refresh`/
`logout`, backed by `IAuthService`/`AuthService`, `IRefreshTokenService`/`RefreshTokenService`,
`IPasswordHasher`/`BCryptPasswordHasher`, `IPasswordPolicyValidator`/`PasswordPolicyValidator`, and
`IJwtTokenService`/`JwtTokenService` (all `Application/Auth/`). ET003 already modeled and migrated
the `PasswordResetOtp` entity (`Domain/Entities/PasswordResetOtp.cs`), its EF configuration, and a
minimal `IPasswordResetOtpRepository`/`PasswordResetOtpRepository` with one method,
`GetActiveForUserAsync`. ET003 also registered all four `RateLimiter` policies (`Register`,
`Login`, `ForgotPassword`, `ResetPassword`) in `AuthServiceCollectionExtensions.AddAuthRateLimiting`
— they share one `AuthRateLimitOptions` config block (`PermitLimit`/`WindowSeconds`) and are already
iterated via `AuthRateLimitPolicyNames.All`. **Only `Register` and `Login` are currently attached**
to a controller action via `[EnableRateLimiting(...)]`; `ForgotPassword`/`ResetPassword` exist as
registered-but-unattached policies, exactly as `auth-rate-limiting`'s existing spec text describes.

**Correction vs. proposal.md**: the proposal describes this ticket as "replacing Swagger UI".
Checking `Program.cs` and `Api.csproj` shows the codebase currently uses only
`Microsoft.AspNetCore.OpenApi` (`AddOpenApi()` / `MapOpenApi()`, .NET 9's native OpenAPI document
generator) with **no Swagger UI package and no UI route wired up at all** — only the raw JSON
document is served at `/openapi/v1.json` in Development. There is nothing to "replace"; Scalar is
purely additive on top of the document that's already generated. This doesn't change the ticket's
scope or the `openapi-documentation-ui` spec's intent (Scalar becomes the interactive UI), so no
spec-delta edit is needed — recorded here as a factual correction, not a silent scope change.

## Goals / Non-Goals

**Goals:**
- `POST /api/auth/forgot-password` and `POST /api/auth/reset-password`, matching
  `openspec/changes/et005-auth-refresh/specs/password-reset-otp/spec.md` exactly.
- Attach the two already-registered rate-limiter policies to the new endpoints.
- Serve Scalar at a UI route backed by the existing native OpenAPI document, Development-only
  (matching the existing `MapOpenApi()` placement inside `if (app.Environment.IsDevelopment())`).
- ADR-0004 recorded in `docs/decisions/`.

**Non-Goals:**
- No change to `RefreshTokenService`'s rotation/reuse-detection logic — reuse
  `IRefreshTokenService.RevokeAllAsync` as-is.
- No new EF Core migration — `PasswordResetOtp` table already exists (ET002).
- No frontend work (ET016 consumes these endpoints later).
- No change to the OpenAPI document's generated content — Scalar reads it unmodified.

## Decisions

**1. New `IPasswordResetOtpService` (Application/Auth), mirroring `IRefreshTokenService`'s shape.**
Encapsulates OTP generation, hashing, expiry, invalidation-of-prior, and verify+consume, keeping
`AuthService` a thin orchestrator (same pattern ET004 already established: `AuthService` composes
`IRefreshTokenService`, `IPasswordHasher`, etc., never touches raw crypto itself).
- `Task RequestResetAsync(Guid userId, CancellationToken ct)` — generates+persists+logs; called
  only when `AuthService.ForgotPasswordAsync` has already resolved an existing user.
- `Task<OtpVerificationResult> VerifyAndConsumeAsync(Guid userId, string plaintextOtp, CancellationToken ct)`.

**2. `AuthService.ForgotPasswordAsync` returns `Task`, not `AuthResult`.**
Per spec, the HTTP response is identical whether the account exists or not, and the controller
never branches on outcome — it always returns `200 OK` after awaiting. Modeling this as a
void-returning service call (instead of a result the controller inspects) makes the
"no branching, no enumeration signal" guarantee structurally obvious at the call site, rather than
relying on every future maintainer to notice an `AuthResult` should always be discarded.
Alternative considered: return `AuthResult` like every other method for consistency — rejected
because it invites a future edit to accidentally branch on `Succeeded`.

**3. Invalidate-prior-OTP by expiring it, not adding an `Invalidated` column.**
`PasswordResetOtp` (`docs/SDS.md` §3.5) has only `ExpiresAt`/`UsedAt`, no separate invalidation
flag, and this ticket anticipates no schema migration. `RequestResetAsync` fetches the current
active OTP via the existing `GetActiveForUserAsync` and, if present, sets its `ExpiresAt` to
`DateTime.UtcNow` before inserting the new row — the old code is now provably unusable (any
verification attempt against it fails the expiry check) without a new column. This is
functionally equivalent to a dedicated "invalidated" flag for this ticket's requirements and
avoids an otherwise-unnecessary migration.

**4. New repository method: `GetMostRecentForUserAsync` (distinct from `GetActiveForUserAsync`).**
`reset-password` must distinguish "expired" (→ `410`) from "wrong/used/never-requested" (→ `401`).
The existing `GetActiveForUserAsync` filters out expired rows entirely, so it can't tell "expired"
apart from "no OTP ever existed". `PasswordResetOtpRepository` gets a second read method that
returns the most recent `PasswordResetOtp` row for a user regardless of expiry/used state;
`PasswordResetOtpService.VerifyAndConsumeAsync` applies the expired/used/mismatch/success branches
itself. This is a repository-contract addition only — no spec-level capability change, since
`refresh-token-lifecycle`/`password-reset-otp` describe service-level behavior, not repository
internals.

**5. OTP failure codes: `410 RESOURCE_EXPIRED` for expired, `401 AUTHENTICATION_FAILED` for
wrong/used/not-found** — per your ET005 clarification answers, added as `AuthFailureReason.OtpExpired`
and `AuthFailureReason.OtpInvalid` (additive enum values; `AuthResult` shape unchanged).

**6. OTP logged via `Console.WriteLine`, not `ILogger`.**
`docs/FRS.md` 3.4.2 / `docs/SDS.md` §4.5 explicitly require the plaintext OTP to reach the server
console (there is no email service, by design, per `docs/FRS.md` §12 Out of Scope) — this is a
deliberate, spec-mandated exception to the general "never log OTPs" principle in `AGENTS.md` §11,
which is aimed at persistent/production logging surfaces, not this dev-only recovery channel.
Writing directly to `Console.Out` (rather than `ILogger<T>`) keeps the OTP off of whatever logging
providers/sinks get added in later tickets (e.g. a file or cloud sink would silently violate
"console only, never persisted" if OTP logging went through `ILogger`). This is called out
explicitly rather than silently reconciling the tension between the two docs.

**7. OTP format: 6-digit numeric string via `RandomNumberGenerator.GetInt32(0, 1_000_000)` formatted
`"D6"` (preserves leading zeros)**, hashed with a local `SHA256.HashData` call — matching
`RefreshTokenService`'s existing hash approach in shape, but implemented as its own private static
helper in `PasswordResetOtpService` rather than extracted into a shared utility. Extracting a
shared hashing helper would require touching `RefreshTokenService` (tested, working ET003 code)
for a ticket that doesn't need to change refresh-token behavior — three near-identical lines is
cheaper than a premature shared abstraction touching unrelated, already-shipped code.

**8. Reuse `IPasswordPolicyValidator`/`IPasswordHasher` unchanged** for the new password in
`reset-password` — identical validation/hashing path as `RegisterRequestValidator`+
`AuthService.RegisterAsync` (`NotEmpty` in the FluentValidation validator, complexity checked in
the service so the same field-level `400 VALIDATION_ERROR` shape as registration's
`PasswordPolicyViolation` applies).

**9. Rate limiting: attribute-only change.** `ForgotPassword`/`ResetPassword` policies are already
registered with concrete limits (`AuthRateLimitOptions`, shared across all four endpoints, exactly
as today). This ticket only adds `[EnableRateLimiting(AuthRateLimitPolicyNames.ForgotPassword)]`
and `[EnableRateLimiting(AuthRateLimitPolicyNames.ResetPassword)]` to the two new controller
actions — no new policy, no config change.

**10. Scalar via `Scalar.AspNetCore` NuGet package, `app.MapScalarApiReference()` immediately after
the existing `app.MapOpenApi()` call, inside the same `if (app.Environment.IsDevelopment())` block.**
Confirmed against current Scalar docs (Context7): the package reads the document `AddOpenApi()`/
`MapOpenApi()` already produce with zero configuration. Default route is `/scalar/v1`.

## Risks / Trade-offs

- [Console-only OTP logging is invisible in production-like environments without console access] →
  Acceptable per `docs/FRS.md` §12 (explicitly out of scope: real email/live OTP delivery); this is
  a documented dev/test-only recovery path, not a production UX gap the ticket is meant to close.
- [Expiring the prior OTP row in-place (Decision 3) means a user who requests two OTPs in quick
  succession never sees the earlier OTP silently disappear from the DB — it simply stops
  validating] → Matches FRS 3.4.6's "only most recently issued OTP is valid" exactly; no data loss,
  the row is retained (audit-visible), just no longer active.
- [`GetMostRecentForUserAsync` returning `null` (account never requested a reset) and returning a
  used/expired row both currently map to generic `401`/`410`, which is intentional for
  anti-enumeration, but means test coverage must explicitly assert the *not-found* case doesn't
  leak a distinguishable status code] → Covered by an explicit "wrong/already-used" scenario in the
  spec; add a dedicated test for the never-requested case too.
- [Scalar is a new third-party NuGet dependency] → Low risk: dev-only tooling, not part of the
  runtime request path for any business endpoint, easy to remove/replace without touching business
  logic.

## Migration Plan

No DB migration. Deployment is additive: new endpoints, new NuGet package, no changes to existing
endpoint contracts or table schemas. Rollback = revert the commit; no data backfill or forward-fix
needed since no schema or existing-row shape changes.

## Open Questions

None outstanding — the two OTP-error-code and rate-limit-reuse questions raised during `/spec` were
resolved in the approved spec delta; the Scalar-vs-Swagger scope question is resolved by Decision
10 and the Context correction above (nothing to replace, Scalar is additive).

## File Plan

**Backend — new files**
- `src/Application/Auth/ForgotPasswordRequest.cs` — `public record ForgotPasswordRequest(string Email);`
- `src/Application/Auth/ForgotPasswordRequestValidator.cs` — `RuleFor(x => x.Email).NotEmpty().EmailAddress();`
- `src/Application/Auth/ResetPasswordRequest.cs` — `public record ResetPasswordRequest(string Email, string Otp, string NewPassword);`
- `src/Application/Auth/ResetPasswordRequestValidator.cs` — `Email` NotEmpty/EmailAddress, `Otp` NotEmpty + `Matches(@"^\d{6}$")`, `NewPassword` NotEmpty (complexity checked in service, matching `RegisterRequestValidator`'s split).
- `src/Application/Auth/IPasswordResetOtpService.cs`
  ```csharp
  public interface IPasswordResetOtpService
  {
      Task RequestResetAsync(Guid userId, CancellationToken cancellationToken);
      Task<OtpVerificationResult> VerifyAndConsumeAsync(Guid userId, string plaintextOtp, CancellationToken cancellationToken);
  }
  ```
- `src/Application/Auth/PasswordResetOtpService.cs` — implements generation/hash/expiry/invalidate-prior/console-log (Decisions 3, 6, 7) and verify/consume (Decision 4).
- `src/Application/Auth/OtpVerificationResult.cs`
  ```csharp
  public enum OtpVerificationFailureReason { None, Expired, Invalid }

  public record OtpVerificationResult(bool Succeeded, OtpVerificationFailureReason FailureReason)
  {
      public static OtpVerificationResult Success() => new(true, OtpVerificationFailureReason.None);
      public static OtpVerificationResult Failure(OtpVerificationFailureReason reason) => new(false, reason);
  }
  ```
- `src/Application/Auth/PasswordResetOtpOptions.cs` — `SectionName = "PasswordReset"`, `int OtpExpiryMinutes { get; set; } = 10;` (mirrors `RefreshTokenOptions` shape).

**Backend — modified files**
- `src/Application/Auth/AuthResult.cs` — add `OtpExpired`, `OtpInvalid` to `AuthFailureReason` (additive).
- `src/Application/Auth/IAuthService.cs` — add:
  ```csharp
  Task ForgotPasswordAsync(ForgotPasswordRequest request, CancellationToken cancellationToken);
  Task<AuthResult> ResetPasswordAsync(ResetPasswordRequest request, CancellationToken cancellationToken);
  ```
- `src/Application/Auth/AuthService.cs` — implement both; `ForgotPasswordAsync` resolves user by
  normalized email via existing `IUserRepository.GetByNormalizedEmailAsync`, no-ops silently if
  absent, else calls `IPasswordResetOtpService.RequestResetAsync`. `ResetPasswordAsync` resolves
  user (fails generic `OtpInvalid` if absent — no enumeration signal), calls
  `IPasswordPolicyValidator.IsSatisfiedBy` first (→ `PasswordPolicyViolation` on failure, no OTP
  consumed), then `IPasswordResetOtpService.VerifyAndConsumeAsync`, then on success updates
  `User.PasswordHash` via `IPasswordHasher.Hash` and calls
  `IRefreshTokenService.RevokeAllAsync(user.Id, ct)` — all inside one `IUnitOfWork.ExecuteInTransactionAsync`.
- `src/Domain/Repositories/IPasswordResetOtpRepository.cs` — add
  `Task<PasswordResetOtp?> GetMostRecentForUserAsync(Guid userId, CancellationToken cancellationToken);`
- `src/Infrastructure/Persistence/Repositories/PasswordResetOtpRepository.cs` — implement it
  (`OrderByDescending(CreatedAt).FirstOrDefaultAsync`, no `UsedAt`/`ExpiresAt` filter).
- `src/Api/Controllers/AuthController.cs` — add `ForgotPassword`/`ResetPassword` actions (inject
  the two new validators), extend `FailureResult` switch: `OtpExpired` → `410 RESOURCE_EXPIRED`,
  `OtpInvalid` → `401 AUTHENTICATION_FAILED`. `ForgotPassword` action has no failure branch — always
  `200 OK` after validation passes.
- `src/Api/Extensions/AuthServiceCollectionExtensions.cs` — register
  `IPasswordResetOtpService`/`PasswordResetOtpService`, `services.Configure<PasswordResetOtpOptions>(...)`.
  No change needed to `AddAuthRateLimiting` (policies already exist).
- `src/Api/appsettings.json` — add `"PasswordReset": { "OtpExpiryMinutes": 10 }`.
- `src/Api/Api.csproj` — add `<PackageReference Include="Scalar.AspNetCore" ... />` (latest stable
  compatible with `net9.0`, confirmed via Context7).
- `src/Api/Program.cs` — `using Scalar.AspNetCore;` + `app.MapScalarApiReference();` immediately
  after `app.MapOpenApi();`, inside the existing `IsDevelopment()` block.

**Docs**
- `docs/decisions/ADR-0004-scalar-openapi-ui.md` — format matches ADR-0001..0003 (Status/Context/
  Decision/Consequences); Context includes the "nothing to replace" correction from this design's
  Context section.

**Tests (backend, `tests/UnitTests` + `tests/IntegrationTests`)**
- Unit: `PasswordResetOtpServiceTests` (generation/hash/expiry/invalidate-prior/console output),
  `AuthServiceTests`-style coverage for `ForgotPasswordAsync`/`ResetPasswordAsync` if such a file
  exists or a new `Application/Auth/AuthServiceResetPasswordTests.cs` otherwise (existing `AuthService`
  coverage today lives only via integration tests per the file scan — follow whichever pattern
  `tests/UnitTests/Application/Auth/` already establishes once confirmed at task-writing time).
- Integration: `AuthForgotPasswordTests.cs`, `AuthResetPasswordTests.cs` (mirroring
  `AuthLoginTests.cs`/`AuthRegisterTests.cs` conventions), covering: existing/non-existing email
  identical response, expired OTP → 410, wrong/used OTP → 401, weak new password → 400, successful
  reset revokes all refresh tokens, rate-limit 429 on both endpoints (extend
  `RateLimitingTests.cs`/`RateLimitedWebApplicationFactory.cs`).

## Build + Test Checkpoints

Run from `backend/` after implementation, in this order (per `AGENTS.md` §Quality Gates):
```bash
dotnet build
dotnet test --filter FullyQualifiedName~UnitTests
dotnet test --filter FullyQualifiedName~IntegrationTests
dotnet format --verify-no-changes
```
No frontend changes in this ticket — frontend gates (`pnpm --filter frontend lint|build|test`,
Playwright) are not applicable to ET005.
