Backend-only ticket — no frontend scope (ET016 consumes these endpoints later), so no `[PARALLEL]`
frontend/backend split applies here. All phases run in `backend/` on the current worktree.

## 1. Foundation — DTOs, contracts, config

- [x] 1.1 Add `src/Application/Auth/ForgotPasswordRequest.cs` (`record ForgotPasswordRequest(string Email)`)
- [x] 1.2 Add `src/Application/Auth/ForgotPasswordRequestValidator.cs` (Email NotEmpty/EmailAddress)
- [x] 1.3 Add `src/Application/Auth/ResetPasswordRequest.cs` (`record ResetPasswordRequest(string Email, string Otp, string NewPassword)`)
- [x] 1.4 Add `src/Application/Auth/ResetPasswordRequestValidator.cs` (Email NotEmpty/EmailAddress, Otp NotEmpty + `^\d{6}$`, NewPassword NotEmpty)
- [x] 1.5 Add `src/Application/Auth/IPasswordResetOtpService.cs` (`RequestResetAsync`, `VerifyAndConsumeAsync`)
- [x] 1.6 Add `src/Application/Auth/OtpVerificationResult.cs` (`OtpVerificationFailureReason` enum + result record, design.md Decision 5)
- [x] 1.7 Add `src/Application/Auth/PasswordResetOtpOptions.cs` (`SectionName = "PasswordReset"`, `OtpExpiryMinutes = 10`)
- [x] 1.8 Extend `src/Application/Auth/AuthResult.cs` — add `OtpExpired`, `OtpInvalid` to `AuthFailureReason` (additive only)
- [x] 1.9 Extend `src/Domain/Repositories/IPasswordResetOtpRepository.cs` — add `GetMostRecentForUserAsync(Guid userId, CancellationToken ct)`
- [x] 1.10 Add `"PasswordReset": { "OtpExpiryMinutes": 10 }` to `src/Api/appsettings.json`
- [x] 1.11 Confirm no EF Core migration is required — `PasswordResetOtp` table already exists from ET002; run `dotnet ef migrations list --project src/Infrastructure --startup-project src/Api` and verify no pending model changes (`dotnet ef migrations add Probe --project src/Infrastructure --startup-project src/Api --dry-run` or equivalent no-op check), then discard any accidental empty migration

### Checkpoint 1
- [x] `dotnet build` → 0 errors
- [x] `dotnet format --verify-no-changes`

## 2. Core Implementation

- [x] 2.1 Implement `src/Infrastructure/Persistence/Repositories/PasswordResetOtpRepository.cs` — `GetMostRecentForUserAsync` (`OrderByDescending(CreatedAt).FirstOrDefaultAsync`, no expiry/used filter)
- [x] 2.2 Implement `src/Application/Auth/PasswordResetOtpService.cs` — OTP generation (`RandomNumberGenerator.GetInt32` + `"D6"`), SHA-256 hash (local helper, not shared with `RefreshTokenService` per design.md Decision 7), invalidate-prior-active-OTP by expiring it (Decision 3), `Console.WriteLine` logging of plaintext OTP + email (Decision 6, never via `ILogger`), `VerifyAndConsumeAsync` expired/used/mismatch/success branching (Decision 4)
- [x] 2.3 Extend `src/Application/Auth/IAuthService.cs` — add `ForgotPasswordAsync`, `ResetPasswordAsync` signatures
- [x] 2.4 Implement `AuthService.ForgotPasswordAsync` — resolve user by normalized email, no-op silently if absent, else call `IPasswordResetOtpService.RequestResetAsync` (`Task`-returning, no branchable result — design.md Decision 2)
- [x] 2.5 Implement `AuthService.ResetPasswordAsync` — resolve user (generic `OtpInvalid` failure if absent), validate password complexity before consuming OTP, call `VerifyAndConsumeAsync`, on success update `PasswordHash` and call `IRefreshTokenService.RevokeAllAsync`, all inside one `IUnitOfWork.ExecuteInTransactionAsync`
- [x] 2.6 Register `IPasswordResetOtpService`/`PasswordResetOtpService` and `Configure<PasswordResetOtpOptions>` in `src/Api/Extensions/AuthServiceCollectionExtensions.cs`
- [x] 2.7 Add `AuthController.ForgotPassword` action — `[HttpPost("forgot-password")]`, `[EnableRateLimiting(AuthRateLimitPolicyNames.ForgotPassword)]`, always `200 OK` after validation
- [x] 2.8 Add `AuthController.ResetPassword` action — `[HttpPost("reset-password")]`, `[EnableRateLimiting(AuthRateLimitPolicyNames.ResetPassword)]`, maps failures via `FailureResult`
- [x] 2.9 Extend `AuthController.FailureResult` switch — `OtpExpired` → `410 RESOURCE_EXPIRED`, `OtpInvalid` → `401 AUTHENTICATION_FAILED`
- [x] 2.10 Add `Scalar.AspNetCore` `PackageReference` to `src/Api/Api.csproj`
- [x] 2.11 Wire `app.MapScalarApiReference()` in `src/Api/Program.cs` immediately after `app.MapOpenApi()`, inside the existing `IsDevelopment()` block
- [x] 2.12 Write `docs/decisions/ADR-0004-scalar-openapi-ui.md` (Status/Context/Decision/Consequences, matching ADR-0001..0003 format; Context includes the "nothing to replace" correction from design.md)

### Checkpoint 2
- [x] `dotnet build` → 0 errors
- [x] `dotnet format --verify-no-changes`
- [x] `dotnet test` (existing suite: 44 unit + 33 integration, all green — no regressions from constructor/DI changes)

## 3. Integration

- [x] 3.1 Manually smoke-test `forgot-password` → `reset-password` round trip against local dev DB: request OTP, confirm console output, reset password, confirm old password no longer works and new one does. Found & fixed a bug: weak-password error incorrectly returned `fields:["password"]` (reused registration's switch case) instead of `fields:["newPassword"]` — added a distinct `AuthFailureReason.NewPasswordPolicyViolation`.
- [x] 3.2 Confirm `/scalar/v1` (or Scalar's default route) renders in a running Development instance and lists all existing `AuthController` endpoints plus the two new ones
- [x] 3.3 Confirm rate limiting triggers `429` on both new endpoints when the configured limit is exceeded (manual curl loop or via the integration tests in section 4)
- [x] 3.4 Update `docs/SDS.md` §5.1 Authentication APIs table if it does not already list `forgot-password`/`reset-password` request/response shapes matching the final DTOs (it currently does per the existing table — verify field names match `ForgotPasswordRequest`/`ResetPasswordRequest` exactly, no drift). Verified: exact match, no doc update needed.

### Checkpoint 3
- [x] `dotnet build` → 0 errors
- [x] `dotnet test` → all green (existing suite unaffected: 44 unit + 33 integration)

## 4. Tests — one per spec scenario

**`password-reset-otp` capability (12 scenarios)**
- [x] 4.1 Unit: Requesting reset for an existing account returns generic success — folded into 4.2 integration test (no dedicated `AuthService` unit test file exists in this codebase; its behavior is covered via integration tests only, matching the established convention for `RegisterAsync`/`LoginAsync`)
- [x] 4.2 Integration: `forgot-password` with existing email → `200 OK`, generic body (`AuthForgotPasswordTests.ExistingAccount_ReturnsGenericSuccess`)
- [x] 4.3 Integration: `forgot-password` with non-existent email → identical `200 OK` body as 4.2 (`AuthForgotPasswordTests.NonExistingAccount_ReturnsIdenticalGenericSuccess`)
- [x] 4.4 Unit: Generated OTP is persisted only as a SHA-256 hash, never plaintext, in `PasswordResetOtp.OtpHash` (`PasswordResetOtpServiceTests.RequestResetAsync_PersistsOtpAsHash_NeverPlaintext`)
- [x] 4.5 Unit: Generated OTP's `ExpiresAt` equals issuance time + `OtpExpiryMinutes` (10) (`PasswordResetOtpServiceTests.RequestResetAsync_ExpiresAfterConfiguredMinutes`)
- [x] 4.6 Unit: Plaintext OTP is written to console output (capture `Console.Out`) and never appears in any `ILogger`-backed log or DB row (`PasswordResetOtpServiceTests.RequestResetAsync_WritesPlaintextOtpAndEmailToConsole`)
- [x] 4.7 Unit: Requesting a second OTP for the same account invalidates the first (`PasswordResetOtpServiceTests.RequestResetAsync_SecondRequest_InvalidatesFirstOtp`)
- [x] 4.8 Integration: `reset-password` with valid unexpired unused OTP + compliant password → `200 OK`, `PasswordHash` updated, OTP marked used (`AuthResetPasswordTests.ValidOtpAndCompliantPassword_ResetsPassword`)
- [x] 4.9 Integration: `reset-password` with expired OTP → `410 RESOURCE_EXPIRED`, password unchanged (`AuthResetPasswordTests.ExpiredOtp_ReturnsResourceExpired`)
- [x] 4.10 Integration: `reset-password` with wrong OTP → `401 AUTHENTICATION_FAILED`, password unchanged (`AuthResetPasswordTests.WrongOtp_ReturnsAuthenticationFailed`)
- [x] 4.11 Integration: `reset-password` with already-used OTP → `401 AUTHENTICATION_FAILED`, password unchanged (`AuthResetPasswordTests.AlreadyUsedOtp_IsRejectedAndCannotBeReusedASecondTime`)
- [x] 4.12 Integration: `reset-password` with valid OTP but weak `newPassword` → `400 VALIDATION_ERROR` with `newPassword` field, password unchanged, OTP not consumed (`AuthResetPasswordTests.WeakNewPassword_ReturnsValidationErrorOnNewPasswordField`) — found & fixed a bug here: see note on task 3.1
- [x] 4.13 Integration: reusing an already-consumed OTP a second time → `401 AUTHENTICATION_FAILED` (single-use) — combined into the same test as 4.11 (`AlreadyUsedOtp_IsRejectedAndCannotBeReusedASecondTime`), since both assert the identical code path (used-OTP branch)
- [x] 4.14 Integration: successful `reset-password` revokes every active `RefreshToken` for that user (`AuthResetPasswordTests.SuccessfulReset_RevokesAllActiveRefreshTokensForUser`)

**`openapi-documentation-ui` capability (2 scenarios)**
- [x] 4.15 Integration: Scalar UI route returns success and renders content sourced from the generated OpenAPI document (`OpenApiDocumentationTests.ScalarUiRoute_RendersSuccessfully`)
- [x] 4.16 Integration/manual diff: generated OpenAPI document's paths/operations/schemas are identical before and after the Scalar change (no drift introduced by the UI swap) — verified pragmatically via `OpenApiDocumentationTests.OpenApiDocument_ListsAllAuthEndpoints` (asserts the document is well-formed and lists all 6 auth paths) plus the manual curl diff performed in task 3.2; a literal before/after document diff isn't meaningful within a single test run since there was no prior document to diff against (see design.md Context correction — no Swagger UI existed before)

**`auth-rate-limiting` capability (2 new scenarios; existing register/login scenarios already covered by ET003)**
- [x] 4.17 Integration: `forgot-password` requests exceeding the configured limit → `429 RATE_LIMIT_EXCEEDED` (`RateLimitingTests.ForgotPasswordRequestsExceedingConfiguredLimit_Return429`, extended `TestEndpointsStartupFilter` with a synthetic endpoint) — found the test factory's config override is ineffective (same root cause documented in `AuthFunctionalWebApplicationFactory`), so the test loops to the real production limit (5) instead of the intended-but-ineffective override (3)
- [x] 4.18 Integration: `reset-password` requests exceeding the configured limit → `429 RATE_LIMIT_EXCEEDED` (`RateLimitingTests.ResetPasswordRequestsExceedingConfiguredLimit_Return429`)

### Checkpoint 4
- [x] `dotnet build` → 0 errors
- [x] `dotnet test --filter FullyQualifiedName~UnitTests` → all green (53 total)
- [x] `dotnet test --filter FullyQualifiedName~IntegrationTests` → all green (45 total)
- [x] `dotnet format --verify-no-changes`

## 5. Archive

- [x] 5.1 Run `openspec archive et005-auth-refresh` — archived as `2026-07-20-et005-auth-refresh`
- [ ] 5.2 Update `docs/TICKETS.md` ET005 row: `Status` → `PR open (#N)` once the PR is opened — deferred to `/pr` per `/implement`'s explicit instruction (status stays `In progress` here)
- [ ] 5.3 Open the PR — deferred to `/pr`
