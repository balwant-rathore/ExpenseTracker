# refresh-token-lifecycle Specification

## Purpose
TBD - created by archiving change et003-auth-foundation. Update Purpose after archive.
## Requirements
### Requirement: Refresh Token Generation
The `Application` layer SHALL provide an `IRefreshTokenService` that generates a cryptographically
random refresh token with a 7-day expiry and persists only its SHA-256 hash via the `RefreshToken`
entity — the raw token value SHALL never be persisted (`docs/FRS.md` §3.2.3, `docs/SDS.md` §4.4).

#### Scenario: Generated refresh token is persisted only as a hash
- **WHEN** `IRefreshTokenService` issues a new refresh token for a user
- **THEN** the persisted `RefreshToken` row SHALL contain a `TokenHash` derived via SHA-256 and
  SHALL NOT contain the raw token value anywhere in the database

#### Scenario: Generated refresh token expires in 7 days
- **WHEN** a refresh token is issued at time T
- **THEN** its persisted `ExpiresAt` SHALL equal T + 7 days

### Requirement: Refresh Token Rotation on Use
Each time a valid, unexpired, unrevoked refresh token is redeemed, the service SHALL issue a new
refresh token and mark the redeemed token as revoked (`RevokedAt` set) — the redeemed token SHALL
NOT be usable again (`docs/SDS.md` §4.4).

#### Scenario: Redeeming a refresh token rotates it
- **WHEN** a valid refresh token is redeemed
- **THEN** a new refresh token SHALL be issued and the redeemed token's `RevokedAt` SHALL be set to
  the current time

#### Scenario: A revoked token cannot be redeemed again
- **WHEN** a refresh token whose `RevokedAt` is already set is redeemed
- **THEN** redemption SHALL fail

### Requirement: Refresh Token Reuse Detection
Redeeming a refresh token that has already been revoked SHALL be treated as token theft/reuse and
SHALL revoke every active (unrevoked, unexpired) refresh token belonging to that token's `User`
(`docs/AGENTS.md` §7).

#### Scenario: Reusing a revoked token revokes all of the user's active tokens
- **WHEN** a refresh token whose `RevokedAt` is already set is redeemed
- **THEN** every other active `RefreshToken` row belonging to the same `User` SHALL have its
  `RevokedAt` set

### Requirement: Bulk Refresh Token Revocation
The `Application` layer SHALL expose a method to revoke all active refresh tokens for a given user,
for use by logout (ET004) and password-reset (ET005) flows.

#### Scenario: Revoking all tokens for a user marks every active token revoked
- **WHEN** bulk revocation is invoked for a `UserId` that has one or more active refresh tokens
- **THEN** every active `RefreshToken` row for that `UserId` SHALL have its `RevokedAt` set, and
  already-revoked or expired rows SHALL be left unchanged

### Requirement: Single Refresh Token Revocation
The `Application` layer SHALL expose a method to revoke exactly one named, active refresh token
belonging to a given user — distinct from the existing bulk-revocation method — for use by the
logout (ET004) flow. Revocation SHALL fail (revoking nothing) if the named token does not belong
to the specified user or is not currently active.

#### Scenario: Revoking a single token sets only that token's RevokedAt
- **WHEN** single-token revocation is invoked for an active refresh token belonging to a user who
  has other active refresh tokens
- **THEN** only the named token's `RevokedAt` SHALL be set; the user's other active refresh tokens
  SHALL be left unchanged

#### Scenario: Revoking a token that does not belong to the caller fails
- **WHEN** single-token revocation is invoked with a token that belongs to a different user than
  the one specified
- **THEN** the operation SHALL fail and no `RefreshToken` row SHALL be modified

