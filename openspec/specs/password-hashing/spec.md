# password-hashing Specification

## Purpose
TBD - created by archiving change et003-auth-foundation. Update Purpose after archive.
## Requirements
### Requirement: Password Hashing
The `Application` layer SHALL provide an `IPasswordHasher` that hashes a plaintext password using
BCrypt before persistence and verifies a plaintext password against a stored hash. The plaintext
password SHALL never be persisted or logged (`docs/FRS.md` §3.1.3, `docs/SDS.md` §4.2).

#### Scenario: Hash and verify roundtrip succeeds
- **WHEN** a plaintext password is hashed via `IPasswordHasher` and then verified against that hash
  with the same plaintext password
- **THEN** verification SHALL succeed

#### Scenario: Verification fails for a non-matching password
- **WHEN** a plaintext password is verified against a hash produced from a different plaintext
  password
- **THEN** verification SHALL fail

### Requirement: Password Complexity Validation
The `Application` layer SHALL provide a reusable password complexity validator enforcing a minimum
of 8 characters with at least one alphabetic character and at least one numeric digit
(`docs/FRS.md` §3.1.2).

#### Scenario: Password shorter than 8 characters is rejected
- **WHEN** a candidate password has fewer than 8 characters
- **THEN** the validator SHALL reject it

#### Scenario: Password missing a digit is rejected
- **WHEN** a candidate password is at least 8 characters and contains only alphabetic characters
- **THEN** the validator SHALL reject it

#### Scenario: Password missing a letter is rejected
- **WHEN** a candidate password is at least 8 characters and contains only numeric digits
- **THEN** the validator SHALL reject it

#### Scenario: Compliant password is accepted
- **WHEN** a candidate password is at least 8 characters and contains at least one letter and one
  digit
- **THEN** the validator SHALL accept it

