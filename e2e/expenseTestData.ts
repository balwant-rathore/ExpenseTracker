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
