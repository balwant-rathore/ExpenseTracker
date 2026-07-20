## ADDED Requirements

### Requirement: JWT Access Token Generation
The `Application` layer SHALL provide an `IJwtTokenService` that issues a JWT access token signed
with HS256, containing exactly one claim — `sub` (the `User.Id`, a Guid) — and expiring 15 minutes
after issuance (`docs/FRS.md` §3.2.1, `docs/SDS.md` §4.3).

#### Scenario: Issued token contains only the sub claim
- **WHEN** `IJwtTokenService` issues an access token for a given `UserId`
- **THEN** the decoded token payload SHALL contain a `sub` claim equal to that `UserId` and SHALL
  contain no `role`, `email`, or any other Employee-derived claim

#### Scenario: Issued token expires after 15 minutes
- **WHEN** an access token is issued at time T
- **THEN** the token's `exp` claim SHALL equal T + 15 minutes

### Requirement: JWT Access Token Validation
The `Application` layer SHALL provide validation of a presented JWT access token, verifying its
HS256 signature against the configured signing key and rejecting the token if the signature is
invalid or the token has expired.

#### Scenario: Expired token is rejected
- **WHEN** a JWT access token is presented after its `exp` timestamp has passed
- **THEN** validation SHALL fail and the request SHALL be treated as unauthenticated

#### Scenario: Tampered token is rejected
- **WHEN** a JWT access token's payload or signature has been altered after issuance
- **THEN** signature validation SHALL fail and the request SHALL be treated as unauthenticated

### Requirement: JWT Signing Key Configuration
The JWT HS256 signing key SHALL be supplied through environment-specific configuration and SHALL
NOT be committed to any `appsettings.*.json` file; for local development it SHALL be set via
`dotnet user-secrets`, matching the existing database-connection-string pattern
(`backend/CLAUDE.md`).

#### Scenario: Signing key is absent from committed configuration files
- **WHEN** any committed `appsettings.*.json` file is inspected
- **THEN** it SHALL NOT contain a literal JWT signing key value
