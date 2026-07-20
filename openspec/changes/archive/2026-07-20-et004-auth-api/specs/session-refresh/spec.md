## ADDED Requirements

### Requirement: Refresh Endpoint Redeems a Refresh Token
`POST /api/auth/refresh` SHALL accept a `refreshToken` and delegate to the existing
`IRefreshTokenService.RedeemAsync` redemption/rotation logic, responding `200 OK` with a new access
token and refresh token pair on success, or `401` if redemption fails for any reason (not-found,
expired, or reuse-detected) (`docs/SDS.md` §5.1).

#### Scenario: Valid refresh token returns a new token pair
- **WHEN** `POST /api/auth/refresh` is called with a refresh token that is valid, unexpired, and
  unrevoked
- **THEN** the response SHALL be `200 OK` containing a new access token and a new refresh token,
  and the redeemed token SHALL be rotated per the existing refresh-token-lifecycle requirements

#### Scenario: Invalid, expired, or reused refresh token is rejected
- **WHEN** `POST /api/auth/refresh` is called with a refresh token that `IRefreshTokenService`
  reports as not-found, expired, or already-revoked (reuse)
- **THEN** the response SHALL be `401 AUTHENTICATION_FAILED`, without distinguishing which of the
  three redemption-failure reasons occurred

### Requirement: Refresh Endpoint Requires No Bearer Token and No Rate Limit
`POST /api/auth/refresh` SHALL NOT require an `Authorization: Bearer` header and SHALL NOT be
subject to any rate-limiter policy, matching the endpoint contract in `docs/SDS.md` §5.1 (no `429`
listed for this endpoint).

#### Scenario: Refresh succeeds without an Authorization header
- **WHEN** `POST /api/auth/refresh` is called with a valid refresh token and no `Authorization`
  header present
- **THEN** the request SHALL be processed normally and SHALL NOT be rejected for missing
  authentication
