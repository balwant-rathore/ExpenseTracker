# user-logout Specification

## Purpose
TBD - created by archiving change et004-auth-api. Update Purpose after archive.
## Requirements
### Requirement: Logout Requires Authentication
`POST /api/auth/logout` SHALL require a valid `Authorization: Bearer` access token; a missing or
invalid access token SHALL be rejected before any refresh-token revocation is attempted
(`docs/SDS.md` §5.1).

#### Scenario: Missing or invalid access token is rejected
- **WHEN** `POST /api/auth/logout` is called without a valid access token
- **THEN** the response SHALL be `401 AUTHENTICATION_FAILED` and no refresh token SHALL be revoked

### Requirement: Logout Revokes Only the Supplied Refresh Token
`POST /api/auth/logout` SHALL revoke exactly the single refresh token supplied in the request
body, and SHALL NOT revoke any other active refresh token belonging to the same user
(`docs/FRS.md` §3.3.1). If the supplied refresh token does not belong to the authenticated caller,
the request SHALL be rejected and no token SHALL be revoked.

#### Scenario: Logout revokes the supplied token and leaves other sessions active
- **WHEN** an authenticated caller with two active refresh tokens (from two separate logins) calls
  logout supplying one of those tokens
- **THEN** the supplied token SHALL be revoked and the other active refresh token for that user
  SHALL remain valid and redeemable

#### Scenario: Revoked token cannot be used to obtain a new access token
- **WHEN** a refresh token has been revoked via logout
- **THEN** a subsequent `POST /api/auth/refresh` call using that token SHALL be rejected with
  `401 AUTHENTICATION_FAILED` (`docs/FRS.md` §3.3.2)

#### Scenario: Supplying a refresh token belonging to a different user is rejected
- **WHEN** an authenticated caller calls logout supplying a refresh token that belongs to a
  different user
- **THEN** the request SHALL be rejected and no refresh token SHALL be revoked

### Requirement: Logout Returns No Content
On successful revocation, `POST /api/auth/logout` SHALL respond `204 No Content`
(`docs/SDS.md` §5.1).

#### Scenario: Successful logout returns 204
- **WHEN** logout revokes the supplied refresh token successfully
- **THEN** the response SHALL be `204 No Content` with an empty body

