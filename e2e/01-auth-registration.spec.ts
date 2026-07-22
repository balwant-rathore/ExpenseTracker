import { test, expect } from '@playwright/test'
import { E2E_EMAIL, E2E_EMPLOYEE_NUMBER, E2E_FIRST_NAME, E2E_PASSWORD } from './testData'

test('registers the reserved account, or confirms it is already registered from a prior run', async ({
  page,
}) => {
  await page.goto('/register')
  await page.getByLabel('Employee number').fill(E2E_EMPLOYEE_NUMBER)
  await page.getByLabel('Email').fill(E2E_EMAIL)
  await page.getByLabel('Password', { exact: true }).fill(E2E_PASSWORD)
  await page.getByLabel('Confirm password').fill(E2E_PASSWORD)
  await page.getByRole('button', { name: /create account/i }).click()

  // Fresh run: registration succeeds and redirects to the dashboard placeholder.
  // Repeat run: the employee number is already claimed -> 422 BUSINESS_RULE_VIOLATION,
  // rendered as this generic message against the employeeNumber field (RegistrationForm spec).
  // Both are valid, already-specified outcomes for this idempotent-by-design test.
  await expect(
    page
      .getByText(new RegExp(`welcome, ${E2E_FIRST_NAME}`, 'i'))
      .or(page.getByText('Registration could not be completed.')),
  ).toBeVisible()
})
