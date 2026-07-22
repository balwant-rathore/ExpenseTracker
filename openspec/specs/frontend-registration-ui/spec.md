# frontend-registration-ui Specification

## Purpose
TBD - created by archiving change et016-authentication-ui. Update Purpose after archive.
## Requirements
### Requirement: Registration Form Submission
The frontend SHALL provide a `/register` route rendering a form that collects `employeeNumber`,
`email`, and `password`, and submits exactly those three fields to `POST /api/auth/register`
(`docs/FRS.md` §3.1; actual `RegisterRequest` DTO shape per `backend/src/Application/Auth/RegisterRequest.cs` —
`docs/SDS.md` §5.1 names this field `employeeId`, which is stale relative to the shipped API).

#### Scenario: Complete, valid form submits successfully
- **WHEN** a user fills `employeeNumber`, `email`, `password` (and a matching confirm-password)
  and submits the registration form
- **THEN** the frontend SHALL call `POST /api/auth/register` with `employeeNumber`, `email`, and
  `password` only — `confirmPassword` SHALL NOT be sent to the API

### Requirement: Client-Side Confirm-Password Check
The registration form SHALL include a client-side-only `confirmPassword` field validated via Zod
to equal `password` before the form can be submitted. This check is a UX convenience and SHALL
NOT be treated as a substitute for backend password validation (`docs/SDS.md` §1.3 "client-side
validation improves UX; server-side validation is authoritative").

#### Scenario: Mismatched confirm-password blocks submission
- **WHEN** a user enters a `password` and a `confirmPassword` that do not match
- **THEN** the frontend SHALL show a validation error on the confirm-password field and SHALL NOT
  call `POST /api/auth/register`

### Requirement: Field-Level Validation Error Display
The frontend SHALL render field-level validation errors returned by
`POST /api/auth/register` (`400 VALIDATION_ERROR` with a `fields` array) against their
corresponding form fields, and SHALL render a duplicate-email conflict as a generic
"email already registered" message without revealing any other detail (`docs/FRS.md` §3.1,
error scenarios).

#### Scenario: Backend returns field-level validation errors
- **WHEN** `POST /api/auth/register` responds `400 VALIDATION_ERROR` with `fields` identifying
  one or more invalid inputs (e.g. password complexity)
- **THEN** the frontend SHALL display each field's error message next to that field

#### Scenario: Backend returns a duplicate-email conflict
- **WHEN** `POST /api/auth/register` responds `409 RESOURCE_CONFLICT` for an already-registered
  email
- **THEN** the frontend SHALL display the generic "email already registered" message returned by
  the backend and SHALL NOT imply any other field was invalid

#### Scenario: Backend rejects an invalid or already-claimed employee number
- **WHEN** `POST /api/auth/register` responds `422 BUSINESS_RULE_VIOLATION` (the supplied
  `employeeNumber` does not match an active, unregistered Employee record)
- **THEN** the frontend SHALL display the backend's generic "registration could not be completed"
  message against the `employeeNumber` field, without implying whether the number doesn't exist,
  belongs to an inactive employee, or is already registered

### Requirement: Successful Registration Establishes Session and Redirects
On a successful registration response, the frontend SHALL establish the authenticated session
(per the `frontend-session-management` capability) and redirect to the authenticated landing
route, since registration logs the user in immediately (`docs/FRS.md` §3.1.3).

#### Scenario: Successful registration redirects to the authenticated landing route
- **WHEN** `POST /api/auth/register` responds `201 Created` with a user, access token, and
  refresh token
- **THEN** the frontend SHALL store the session and navigate the user to `/dashboard` without
  requiring a separate login step

