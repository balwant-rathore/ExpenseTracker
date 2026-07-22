## ADDED Requirements

### Requirement: Forgot-Password Request Step
The frontend SHALL provide a `/forgot-password` route rendering a form that collects `email` and
submits it to `POST /api/auth/forgot-password`, then displays an identical generic confirmation
message regardless of whether the account exists (`docs/FRS.md` §3.4.1).

#### Scenario: Any submitted email shows the same generic confirmation
- **WHEN** `POST /api/auth/forgot-password` responds `200 OK` for any submitted email
- **THEN** the frontend SHALL display the same generic "if an account exists, an OTP has been
  issued" message, with no indication of whether the account exists

#### Scenario: Rate-limited request shows a generic throttling message
- **WHEN** `POST /api/auth/forgot-password` responds `429 RATE_LIMIT_EXCEEDED`
- **THEN** the frontend SHALL display a generic "too many attempts, try again later" message

### Requirement: Reset-Password Step
After the forgot-password step, the frontend SHALL present a second screen collecting `otp` and
`newPassword` (plus a client-side-only `confirmNewPassword`) and submit `email`, `otp`, and
`newPassword` to `POST /api/auth/reset-password` (`docs/FRS.md` §3.4.3, §3.4.4).

#### Scenario: Valid OTP and compliant new password reset successfully
- **WHEN** a user enters the OTP and a complexity-compliant new password (with matching confirm)
  and submits the reset step
- **THEN** the frontend SHALL call `POST /api/auth/reset-password` with `email`, `otp`, and
  `newPassword` only, and on `200 OK` SHALL navigate the user to `/login` with a success message

#### Scenario: Mismatched confirm-new-password blocks submission
- **WHEN** `newPassword` and `confirmNewPassword` do not match
- **THEN** the frontend SHALL show a validation error on the confirm field and SHALL NOT call
  `POST /api/auth/reset-password`

### Requirement: Reset Error Display
The frontend SHALL render OTP/reset error responses without revealing which specific check
failed beyond what the backend's error code already distinguishes (`docs/FRS.md` §3.4, error
scenarios).

#### Scenario: Expired OTP is shown as an expiry-specific message
- **WHEN** `POST /api/auth/reset-password` responds `410 RESOURCE_EXPIRED`
- **THEN** the frontend SHALL display a message indicating the OTP has expired and offer a way
  to request a new one (return to the forgot-password step)

#### Scenario: Wrong or already-used OTP shows a generic authentication error
- **WHEN** `POST /api/auth/reset-password` responds `401 AUTHENTICATION_FAILED`
- **THEN** the frontend SHALL display a generic "invalid or already-used code" message without
  distinguishing between "wrong OTP" and "already used"

#### Scenario: Weak new password shows field-level detail
- **WHEN** `POST /api/auth/reset-password` responds `400 VALIDATION_ERROR` for `newPassword`
- **THEN** the frontend SHALL display the returned field-level message next to the new-password
  field
