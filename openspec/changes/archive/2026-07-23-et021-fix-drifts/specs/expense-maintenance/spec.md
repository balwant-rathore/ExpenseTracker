## MODIFIED Requirements

### Requirement: Edit Field-Level and Business-Rule Validation
`PUT /api/expenses/{id}` SHALL re-run, identically to expense creation (`docs/FRS.md`
§4.1.1), every field-level check (`category` must be one of the seven `ExpenseCategory`
values; an omitted `currency` defaults to `INR` while an explicitly-supplied non-`INR`
`currency` is rejected, matching the `expense-submission` capability's Field-Level Validation
requirement; `description` must not exceed 500 characters) and every business-rule check
(`amount` greater than zero — **BR-01**; `expenseDate` not after the current company-local date
— **BR-02**, **BR-10**) against the edited values, rejecting any violation and persisting no
change.

#### Scenario: Invalid category is rejected
- **WHEN** an edit request's `category` is not one of the seven defined `ExpenseCategory`
  values
- **THEN** the response is `400` with code `VALIDATION_ERROR` and a `fields` entry for
  `category`

#### Scenario: Omitted currency defaults to INR
- **WHEN** an edit request omits `currency` entirely and every other field is valid
- **THEN** the response is `200`, and the expense's stored `Currency` is `INR`

#### Scenario: Non-INR currency is rejected
- **WHEN** an edit request's `currency` is explicitly supplied as anything other than `INR`
- **THEN** the response is `400` with code `VALIDATION_ERROR` and a `fields` entry for
  `currency`

#### Scenario: Description over 500 characters is rejected
- **WHEN** an edit request's `description` exceeds 500 characters
- **THEN** the response is `400` with code `VALIDATION_ERROR` and a `fields` entry for
  `description`

#### Scenario: Zero or negative amount is rejected
- **WHEN** an edit request has `amount <= 0`
- **THEN** the response is `422` with code `BUSINESS_RULE_VIOLATION`, and no change is
  persisted

#### Scenario: Future expense date is rejected
- **WHEN** an edit request has `expenseDate` after the current company-local date
- **THEN** the response is `422` with code `BUSINESS_RULE_VIOLATION`, and no change is
  persisted
