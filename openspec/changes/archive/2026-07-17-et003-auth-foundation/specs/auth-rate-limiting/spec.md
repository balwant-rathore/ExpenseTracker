## ADDED Requirements

### Requirement: Rate Limiter Policies for Auth Endpoints
The `Api` layer SHALL register named ASP.NET Core `RateLimiter` policies, one per auth endpoint
(`register`, `login`, `forgot-password`, `reset-password`), using a sliding-window algorithm keyed
per caller (IP and/or email), with placeholder configured limits pending real endpoint
availability in ET004/ET005 (`docs/FRS.md` §3.5, `docs/SDS.md` §4.7). The policies SHALL be
registered and ready to attach via attribute; this ticket adds no endpoints that use them.

#### Scenario: Requests within the configured limit are permitted
- **WHEN** the number of requests from a given caller within the configured sliding window is at
  or below the configured limit
- **THEN** each request SHALL be permitted to proceed to the rate limiter's downstream pipeline

#### Scenario: Requests exceeding the configured limit are rejected
- **WHEN** a caller exceeds the configured request limit within the sliding window for a given
  policy
- **THEN** the exceeding request SHALL be rejected with HTTP `429` and error code
  `RATE_LIMIT_EXCEEDED`

### Requirement: Rate Limiting Does Not Leak Credential Validity
A rejected (`429`) request SHALL receive an identical response regardless of whether the
credentials or identifier supplied in that request would otherwise have been valid, preserving the
anti-enumeration guarantees required of the auth endpoints (`docs/FRS.md` §3.5, error scenarios;
`docs/AGENTS.md` §7).

#### Scenario: Rate-limited response does not reveal credential correctness
- **WHEN** a request is rejected for exceeding a rate limit
- **THEN** the `429` response body SHALL be the same standard error envelope regardless of whether
  the request's credentials would have succeeded or failed authentication
