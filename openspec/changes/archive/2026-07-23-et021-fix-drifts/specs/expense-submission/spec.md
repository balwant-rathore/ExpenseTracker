## MODIFIED Requirements

### Requirement: Field-Level Validation
The `Application` layer SHALL reject, identically for `Draft` and `Submitted`, any request whose
`Category` is not one of the seven `ExpenseCategory` values, or whose `Description` exceeds 500
characters (`docs/FRS.md` §4.1.1), with `400 VALIDATION_ERROR` and field-level `fields` detail. No
`Expense` row SHALL be created on failure.

For `Currency` (`docs/FRS.md` §4.1.1 "Default to Rupees - INR" — no multi-currency support is in
scope): an omitted `currency` SHALL default to `INR` rather than being rejected; a `currency`
that is explicitly supplied but is anything other than `INR` SHALL still be rejected with
`400 VALIDATION_ERROR` and a `fields` entry for `currency`.

#### Scenario: Invalid category is rejected
- **WHEN** a request's `category` is not one of the seven defined `ExpenseCategory` values
- **THEN** the response is `400` with code `VALIDATION_ERROR` and a `fields` entry for `category`

#### Scenario: Omitted currency defaults to INR
- **WHEN** a request omits `currency` entirely and every other field is valid
- **THEN** the response is `201 Created` and the created `Expense` row has `Currency` set to
  `INR`

#### Scenario: Non-INR currency is rejected
- **WHEN** a request's `currency` is explicitly supplied as anything other than `INR`
- **THEN** the response is `400` with code `VALIDATION_ERROR` and a `fields` entry for `currency`

#### Scenario: Description over 500 characters is rejected
- **WHEN** a request's `description` exceeds 500 characters
- **THEN** the response is `400` with code `VALIDATION_ERROR` and a `fields` entry for
  `description`
