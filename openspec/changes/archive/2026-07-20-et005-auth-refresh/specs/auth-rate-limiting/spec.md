## MODIFIED Requirements

### Requirement: Rate Limiter Policies for Auth Endpoints
The `Api` layer SHALL register named ASP.NET Core `RateLimiter` policies, one per auth endpoint
(`register`, `login`, `forgot-password`, `reset-password`), using a sliding-window algorithm keyed
per caller (IP and/or email), with concrete configured limits, and SHALL attach the
`forgot-password` and `reset-password` policies to their respective endpoints via
`[EnableRateLimiting]` (`docs/FRS.md` §3.5, `docs/SDS.md` §4.7). All four policies are now attached
to live endpoints — no policy remains unattached pending a future ticket.

#### Scenario: Requests within the configured limit are permitted
- **WHEN** the number of requests from a given caller within the configured sliding window is at
  or below the configured limit
- **THEN** each request SHALL be permitted to proceed to the rate limiter's downstream pipeline

#### Scenario: Requests exceeding the configured limit are rejected
- **WHEN** a caller exceeds the configured request limit within the sliding window for a given
  policy
- **THEN** the exceeding request SHALL be rejected with HTTP `429` and error code
  `RATE_LIMIT_EXCEEDED`

#### Scenario: Forgot-password requests exceeding the limit are rejected
- **WHEN** a caller exceeds the configured request limit within the sliding window for the
  `forgot-password` policy
- **THEN** the exceeding request to `POST /api/auth/forgot-password` SHALL be rejected with HTTP
  `429` and error code `RATE_LIMIT_EXCEEDED`

#### Scenario: Reset-password requests exceeding the limit are rejected
- **WHEN** a caller exceeds the configured request limit within the sliding window for the
  `reset-password` policy
- **THEN** the exceeding request to `POST /api/auth/reset-password` SHALL be rejected with HTTP
  `429` and error code `RATE_LIMIT_EXCEEDED`
