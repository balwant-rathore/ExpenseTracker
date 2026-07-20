## ADDED Requirements

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
