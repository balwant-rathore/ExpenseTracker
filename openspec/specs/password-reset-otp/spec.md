# password-reset-otp Specification

## Purpose
TBD - created by archiving change et005-auth-refresh. Update Purpose after archive.
## Requirements
### Requirement: Forgot Password Request Does Not Leak Account Existence
`POST /api/auth/forgot-password` SHALL accept an `email` and return an identical `200 OK` response
whether or not an account with that email exists (`docs/FRS.md` 3.4.1).

#### Scenario: Requesting reset for an existing account returns generic success
- **WHEN** `forgot-password` is called with an email that matches an existing `User`
- **THEN** the response SHALL be `200 OK` with a generic body containing no account-existence
  signal

#### Scenario: Requesting reset for a non-existent account returns the same generic success
- **WHEN** `forgot-password` is called with an email that matches no existing `User`
- **THEN** the response SHALL be `200 OK` with a body identical in shape and content to the
  existing-account case

### Requirement: OTP Generation, Hashing, and Console Logging
When the account exists, `forgot-password` SHALL generate a 6-digit numeric OTP, persist only its
SHA-256 hash via `PasswordResetOtp` with a 10-minute expiry, and log the plaintext OTP together
with the target email to the server console — never to persistent storage or any other log sink
(`docs/FRS.md` 3.4.2, `docs/SDS.md` §4.5).

#### Scenario: OTP is persisted only as a hash
- **WHEN** an OTP is generated for an existing account
- **THEN** the persisted `PasswordResetOtp` row SHALL contain an `OtpHash` derived via SHA-256 and
  SHALL NOT contain the plaintext OTP anywhere in the database

#### Scenario: OTP expires 10 minutes after issuance
- **WHEN** an OTP is generated at time T
- **THEN** its persisted `ExpiresAt` SHALL equal T + 10 minutes

#### Scenario: Plaintext OTP is written to console only
- **WHEN** an OTP is generated for an existing account
- **THEN** the plaintext OTP value SHALL be written to the server console output and SHALL NOT
  appear in any persisted log file, database row, or API response

### Requirement: New OTP Invalidates Prior Unused OTP
Requesting a new OTP for an account SHALL invalidate any previously issued, unused OTP for that
same account — only the most recently issued OTP is valid at any time (`docs/FRS.md` 3.4.6).

#### Scenario: Requesting a second OTP invalidates the first
- **WHEN** an account has an existing unused, unexpired OTP and `forgot-password` is called again
  for that account
- **THEN** the previously issued OTP SHALL no longer be usable to reset the password, and only the
  newly issued OTP SHALL be valid

### Requirement: Reset Password Validates OTP and Updates Credentials
`POST /api/auth/reset-password` SHALL accept `email`, `otp`, and `newPassword`; SHALL verify the
OTP is the most recently issued one for that account, unexpired, and unused; SHALL validate
`newPassword` against the existing password-complexity validator; and on success SHALL update the
user's password hash, mark the OTP as used, and return `200 OK` (`docs/FRS.md` 3.4.3, 3.4.4,
`docs/SDS.md` §5.1).

#### Scenario: Valid OTP and compliant password resets the password
- **WHEN** `reset-password` is called with a matching, unexpired, unused OTP and a
  complexity-compliant `newPassword`
- **THEN** the user's `PasswordHash` SHALL be updated to the new password's hash, the OTP SHALL be
  marked used, and the response SHALL be `200 OK`

#### Scenario: Expired OTP is rejected
- **WHEN** `reset-password` is called with an OTP whose `ExpiresAt` has passed
- **THEN** the request SHALL be rejected with `410 RESOURCE_EXPIRED` and the password SHALL NOT be
  changed

#### Scenario: Wrong or already-used OTP is rejected
- **WHEN** `reset-password` is called with an OTP that does not match the account's current valid
  OTP hash, or whose `UsedAt` is already set
- **THEN** the request SHALL be rejected with `401 AUTHENTICATION_FAILED` and the password SHALL
  NOT be changed

#### Scenario: Weak new password is rejected
- **WHEN** `reset-password` is called with an otherwise-valid OTP but a `newPassword` that fails
  the password-complexity validator
- **THEN** the request SHALL be rejected with `400 VALIDATION_ERROR` including field-level detail
  for `newPassword`, and the password SHALL NOT be changed

#### Scenario: OTP is single-use
- **WHEN** an OTP that was already successfully consumed by a prior `reset-password` call is
  submitted again
- **THEN** the request SHALL be rejected with `401 AUTHENTICATION_FAILED` and the password SHALL
  NOT be changed

### Requirement: Successful Reset Revokes All Refresh Tokens
On a successful password reset, all of the user's active refresh tokens SHALL be revoked, forcing
re-login on every device (`docs/FRS.md` 3.4.5), using the existing bulk refresh-token revocation
method (`refresh-token-lifecycle` capability).

#### Scenario: Successful reset revokes every active refresh token for the user
- **WHEN** `reset-password` succeeds for a user who has one or more active refresh tokens
- **THEN** every active `RefreshToken` row for that user SHALL have its `RevokedAt` set

