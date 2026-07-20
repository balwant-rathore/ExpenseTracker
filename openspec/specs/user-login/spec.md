# user-login Specification

## Purpose
TBD - created by archiving change et004-auth-api. Update Purpose after archive.
## Requirements
### Requirement: Login Credential Verification
`POST /api/auth/login` SHALL accept `email` and `password`, verify the password against the
stored hash for the `User` matching that email (case-insensitively), and on match issue a new
access token and refresh token pair (`docs/FRS.md` §3.2.1).

#### Scenario: Correct credentials issue a token pair
- **WHEN** login is submitted with an email matching an existing `User` and the correct password
  for that account
- **THEN** the response SHALL be `200 OK` containing a `User` object (id, email, employeeNumber,
  firstName, lastName, role), a new access token, and a new refresh token

### Requirement: Generic Login Failure Error
`POST /api/auth/login` SHALL return the identical generic `AUTHENTICATION_FAILED` error whether
the supplied email does not match any `User` or the password does not match the stored hash for a
matching `User` — the response SHALL NOT indicate which of the two failed (`docs/FRS.md` §3.2.2).

#### Scenario: Unknown email returns the generic error
- **WHEN** login is submitted with an email that matches no existing `User`
- **THEN** the response SHALL be `401 AUTHENTICATION_FAILED` with the same generic message used
  for a wrong password

#### Scenario: Wrong password returns the identical generic error
- **WHEN** login is submitted with an email matching an existing `User` but an incorrect password
- **THEN** the response SHALL be `401 AUTHENTICATION_FAILED` with the same generic message used
  for an unknown email

#### Scenario: Missing required fields return a validation error
- **WHEN** login is submitted without an `email` or without a `password`
- **THEN** the response SHALL be `400 VALIDATION_ERROR` identifying the missing field(s)

### Requirement: Login Issues a Server-Persisted Refresh Token
On successful login, the issued refresh token SHALL be persisted server-side (as a SHA-256 hash,
per the existing `refresh-token-lifecycle` capability) so it can later be revoked or rotated
(`docs/FRS.md` §3.2.3).

#### Scenario: Successful login persists a redeemable refresh token
- **WHEN** login succeeds
- **THEN** the issued refresh token SHALL be redeemable exactly once against
  `POST /api/auth/refresh` before rotation, consistent with the existing refresh-token-lifecycle
  requirements

