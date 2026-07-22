# frontend-route-guards Specification

## Purpose
TBD - created by archiving change et016-authentication-ui. Update Purpose after archive.
## Requirements
### Requirement: Authentication Gate Redirects Unauthenticated Users
The frontend SHALL provide a route-guard wrapper that renders its child route only when an
authenticated session exists, and otherwise redirects to `/login` (`docs/SDS.md` §4.1, §4.6).

#### Scenario: Unauthenticated user is redirected away from a protected route
- **WHEN** a user with no active session navigates directly to a protected route (e.g.
  `/dashboard`)
- **THEN** the frontend SHALL redirect to `/login` without rendering the protected route's
  content

#### Scenario: Authenticated user reaches the protected route
- **WHEN** a user with an active session navigates to a protected route
- **THEN** the frontend SHALL render that route's content

#### Scenario: In-flight silent refresh defers the redirect decision
- **WHEN** a user navigates to a protected route while the startup silent-refresh call (per
  `frontend-session-management`) is still pending
- **THEN** the frontend SHALL wait for that call to resolve before deciding to render the route
  or redirect to `/login`

### Requirement: Role-Based Route Restriction
The frontend SHALL provide a route-guard wrapper that additionally restricts a route to one or
more `EmployeeRole` values, read from the authenticated user object returned by
login/register/refresh — never decoded from the JWT access token itself
(`docs/SDS.md` §4.1 "Roles are resolved from the Employee record"; AGENTS.md §11 "Never trust
roles from the JWT").

#### Scenario: User with an allowed role reaches the restricted route
- **WHEN** an authenticated user whose `role` is included in a route's allowed-roles list
  navigates to that route
- **THEN** the frontend SHALL render that route's content

#### Scenario: User with a disallowed role is blocked
- **WHEN** an authenticated user whose `role` is not included in a route's allowed-roles list
  navigates to that route
- **THEN** the frontend SHALL NOT render that route's content and SHALL redirect the user away
  from it (e.g. to `/dashboard`)

#### Scenario: Role check never reads the JWT payload
- **WHEN** the role-based route guard evaluates whether the current user may access a route
- **THEN** it SHALL use only the `role` value from the authenticated user object already returned
  by the auth API, and SHALL NOT decode or read any claim from the JWT access token itself

