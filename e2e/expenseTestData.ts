// Reserved seed rows (docs/EmployeeSeedData.csv) for expense E2E use only — kept separate
// from testData.ts's auth-flow reservation (EMP017) so the two suites never collide on the
// same account. EMP024/EMP014 were tried first but turned out to already be registered from
// prior manual testing against this persistent dev database (unknown passwords); a direct DB
// check (no Users row) confirmed EMP050 (Audrey) and its manager EMP010 (Charlotte) were both
// still genuinely fresh.
export const EXPENSE_E2E_EMPLOYEE_NUMBER = 'EMP050'
export const EXPENSE_E2E_EMAIL = 'e2e-check-050@company.com'
export const EXPENSE_E2E_PASSWORD = 'Password1'
export const EXPENSE_E2E_FIRST_NAME = 'Audrey'

export const EXPENSE_E2E_MANAGER_EMPLOYEE_NUMBER = 'EMP010'
export const EXPENSE_E2E_MANAGER_EMAIL = 'e2e-check-010@company.com'
export const EXPENSE_E2E_MANAGER_PASSWORD = 'Password1'
export const EXPENSE_E2E_MANAGER_FIRST_NAME = 'Charlotte'

// Reserved for ET018 review-workflow E2E coverage — EMP001 (Sarah, ComplianceOfficer) is the
// first seeded ComplianceOfficer row and was confirmed fresh by successfully registering during
// ET018. EMP002/EMP003 (Michael/Emily, Finance) both turned out to already have a Users row from
// prior manual testing (same trap EMP024/EMP014 hit above — logging in with an assumed password
// failed with "Invalid email or password"); EMP004 (David) was genuinely fresh.
export const EXPENSE_E2E_COMPLIANCE_EMPLOYEE_NUMBER = 'EMP001'
export const EXPENSE_E2E_COMPLIANCE_EMAIL = 'e2e-check-001@company.com'
export const EXPENSE_E2E_COMPLIANCE_PASSWORD = 'Password1'
export const EXPENSE_E2E_COMPLIANCE_FIRST_NAME = 'Sarah'

export const EXPENSE_E2E_FINANCE_EMPLOYEE_NUMBER = 'EMP004'
export const EXPENSE_E2E_FINANCE_EMAIL = 'e2e-check-004@company.com'
export const EXPENSE_E2E_FINANCE_PASSWORD = 'Password1'
export const EXPENSE_E2E_FINANCE_FIRST_NAME = 'David'
