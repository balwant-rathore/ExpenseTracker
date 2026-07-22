# frontend-login-ui Specification

## Purpose
TBD - created by archiving change et016-authentication-ui. Update Purpose after archive.
## Requirements
### Requirement: Login Form Submission
The frontend SHALL provide a `/login` route rendering a form that collects `email` and
`password` and submits them to `POST /api/auth/login` (`docs/FRS.md` §3.2, `docs/SDS.md` §5.1).

#### Scenario: Valid credentials submit successfully
- **WHEN** a user enters an email and password and submits the login form
- **THEN** the frontend SHALL call `POST /api/auth/login` with those exact values and no
  additional fields

#### Scenario: Empty fields are blocked before submission
- **WHEN** a user submits the login form with `email` or `password` left empty
- **THEN** the frontend SHALL show a client-side required-field error and SHALL NOT call
  `POST /api/auth/login`

### Requirement: Generic Authentication Error Display
The frontend SHALL render the backend's `401 AUTHENTICATION_FAILED` login error as a single
generic message and SHALL NOT infer or display which field (email or password) was incorrect
(`docs/FRS.md` §3.2.2).

#### Scenario: Backend rejects invalid credentials
- **WHEN** `POST /api/auth/login` responds with `401 AUTHENTICATION_FAILED`
- **THEN** the frontend SHALL display one generic "invalid email or password" message and SHALL
  NOT attach the error to either the email field or the password field specifically

#### Scenario: Backend rejects with rate limit
- **WHEN** `POST /api/auth/login` responds with `429 RATE_LIMIT_EXCEEDED`
- **THEN** the frontend SHALL display a generic "too many attempts, try again later" message
  without revealing whether the submitted credentials were correct

### Requirement: Successful Login Establishes Session and Redirects
On a successful login response, the frontend SHALL establish the authenticated session (per the
`frontend-session-management` capability) and redirect to the authenticated landing route
(`docs/SDS.md` §5.1 "All users land to dashboard after successful login").

#### Scenario: Successful login redirects to the authenticated landing route
- **WHEN** `POST /api/auth/login` responds `200 OK` with a user, access token, and refresh token
- **THEN** the frontend SHALL store the session and navigate the user to `/dashboard`, regardless
  of the user's `role`

