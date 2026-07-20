## ADDED Requirements

### Requirement: Employee Number Validation for Registration
`POST /api/auth/register` SHALL validate the supplied `employeeNumber` against a pre-seeded
`Employee` record's `EmployeeNumber`, and SHALL reject registration with a generic error if no
matching record exists, the matching record's `IsActive` is `false`, or the matching record
already has a linked `User` account — all three cases SHALL return the identical error so neither
existence, active-status, nor prior-registration-status of the employee number is revealed
(`docs/FRS.md` §3.1, `docs/AGENTS.md` §7; `docs/decisions/ADR-0003-registration-employeenumber-field.md`).
This check SHALL occur before any database write, so an already-registered employee number never
surfaces as a raw uniqueness-constraint failure.

#### Scenario: Unknown employee number is rejected
- **WHEN** registration is submitted with an `employeeNumber` that matches no seeded `Employee`
- **THEN** the request SHALL be rejected with a generic registration-failure error, not a
  field-specific "employee not found" message

#### Scenario: Inactive employee's number is rejected with the same generic error
- **WHEN** registration is submitted with an `employeeNumber` that matches a seeded `Employee`
  whose `IsActive` is `false`
- **THEN** the request SHALL be rejected with the identical generic error used for an unknown
  employee number

#### Scenario: Already-registered employee number is rejected with the same generic error
- **WHEN** registration is submitted with an `employeeNumber` that matches a seeded, active
  `Employee` who already has a linked `User` account (regardless of the email supplied)
- **THEN** the request SHALL be rejected with the identical generic error used for an unknown
  employee number, and SHALL NOT reach a database write that could raise a uniqueness-constraint
  error

#### Scenario: Active, not-yet-registered employee's number allows registration to proceed
- **WHEN** registration is submitted with an `employeeNumber` that matches a seeded, active
  `Employee` who has no linked `User` account yet
- **THEN** the employee-number check SHALL pass and registration SHALL proceed to email/password
  validation

### Requirement: Registration Email Uniqueness
`POST /api/auth/register` SHALL reject registration if a `User` already exists with the supplied
email, compared case-insensitively, without revealing that the conflict is specifically the email
field beyond a generic "already registered" message (`docs/FRS.md` §3.1.1, error scenarios).

#### Scenario: Duplicate case-insensitive email is rejected
- **WHEN** registration is submitted with an email that differs only in case from an existing
  `User.Email`
- **THEN** the request SHALL be rejected with a `409` and an error that states the email is
  already registered, without listing any other field as invalid

#### Scenario: Unique email is accepted
- **WHEN** registration is submitted with an email not used by any existing `User`
- **THEN** the email-uniqueness check SHALL pass

### Requirement: Registration Password Complexity Enforcement
`POST /api/auth/register` SHALL reject a password that fails the existing password complexity
policy (minimum 8 characters, at least one letter, at least one digit), returning a field-level
validation error identifying the password field (`docs/FRS.md` §3.1.2).

#### Scenario: Non-compliant password is rejected with a field-level error
- **WHEN** registration is submitted with a password that fails the complexity policy
- **THEN** the request SHALL be rejected with a `400 VALIDATION_ERROR` whose `fields` array
  identifies the `password` field

### Requirement: Successful Registration Creates Account and Immediately Logs In
On passing all validation, `POST /api/auth/register` SHALL hash the password (never persisting or
returning the plaintext or hash), create the `User` account linked to the matched `Employee`, and
respond `201 Created` with a `User` representation plus a newly issued access token and refresh
token pair, equivalent to an immediate login (`docs/FRS.md` §3.1.3, §3.1.4).

#### Scenario: Successful registration returns tokens and a user without the password hash
- **WHEN** registration passes employee-number, email-uniqueness, and password-complexity checks
- **THEN** the response SHALL be `201 Created` containing a `User` object (id, email,
  employeeNumber, firstName, lastName, role) and both an access token and a refresh token, and the
  response body SHALL NOT contain the password or its hash anywhere

#### Scenario: Password is persisted only as a hash
- **WHEN** a registration succeeds
- **THEN** the persisted `User.PasswordHash` SHALL be a BCrypt hash of the submitted password and
  the plaintext password SHALL NOT be persisted or logged anywhere
