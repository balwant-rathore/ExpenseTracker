## ADDED Requirements

### Requirement: Per-Request Employee Role Resolution
After JWT bearer authentication succeeds, middleware SHALL load the caller's `Employee` record via
the `sub` claim's `UserId` and attach the resolved `EmployeeRole` as a claim on the current
request's `ClaimsPrincipal`. The role SHALL be re-derived from the `Employee` record on every
request and SHALL NOT be read from the JWT itself (`docs/AGENTS.md` §7, §11; `docs/SDS.md` §4.1,
§4.6).

#### Scenario: Role is resolved fresh on every authenticated request
- **WHEN** two consecutive requests are made with the same valid access token, and the caller's
  `Employee.Role` changes in the database between the two requests
- **THEN** the second request's resolved role SHALL reflect the updated `Employee.Role`, not any
  value cached from the first request or embedded in the token

#### Scenario: Middleware attaches the resolved role claim
- **WHEN** a request carries a valid access token for a `User` whose `Employee.Role` is `Manager`
- **THEN** the request's `ClaimsPrincipal` SHALL carry a role claim of `Manager` after the
  middleware runs

### Requirement: Unresolvable Employee Rejected
If the authenticated `User`'s linked `Employee` record cannot be found or the `Employee` is
inactive (`IsActive = false`), the middleware SHALL treat the request as unauthenticated rather
than resolving a role, returning a generic authentication failure (`docs/AGENTS.md` §7, error
contract in `AGENTS.md` §6).

#### Scenario: Missing Employee record fails authentication
- **WHEN** a valid access token's `User` has no linked `Employee` record
- **THEN** the request SHALL be rejected as unauthenticated with the generic
  `AUTHENTICATION_FAILED` error, not a role-specific error

#### Scenario: Inactive Employee fails authentication
- **WHEN** a valid access token's linked `Employee.IsActive` is `false`
- **THEN** the request SHALL be rejected as unauthenticated with the generic
  `AUTHENTICATION_FAILED` error

### Requirement: Role-Based Authorization Policies
The `Api` layer SHALL register one named ASP.NET Core authorization policy per `EmployeeRole`
(`Employee`, `Manager`, `Finance`, `ComplianceOfficer`), consumable via
`[Authorize(Policy = "...")]`, so that no controller contains an inline `if (role == ...)` check
(`backend/CLAUDE.md`).

#### Scenario: Policy allows a matching role
- **WHEN** a request whose resolved role satisfies a given role policy hits an endpoint decorated
  with that policy
- **THEN** the request SHALL be authorized to proceed

#### Scenario: Policy rejects a non-matching role
- **WHEN** a request whose resolved role does not satisfy a given role policy hits an endpoint
  decorated with that policy
- **THEN** the request SHALL be rejected with a `403` `AUTHORIZATION_FAILED` error
