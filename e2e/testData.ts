// Reserved Role=Employee seed row (docs/EmployeeSeedData.csv) for E2E use only — see
// design.md D7 Open Question 3. Do not reuse this employeeNumber/email in other tests or in the
// manual smoke pass (which used EMP024 instead, to keep the two data sets separate).
//
// EMP015/EMP016 were tried first but turned out to already be registered from years of prior
// manual ticket testing against this persistent dev database — EMP017 ("Liam Allen") was the
// first genuinely fresh row found by probing, and is now registered with the credentials below.
export const E2E_EMPLOYEE_NUMBER = 'EMP017'
export const E2E_EMAIL = 'e2e-check-017@company.com'
export const E2E_PASSWORD = 'Password1'
export const E2E_FIRST_NAME = 'Liam'
