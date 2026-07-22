import { test, expect } from '@playwright/test'
import { registerOrLogin } from './authHelpers'
import {
  EXPENSE_E2E_MANAGER_EMAIL,
  EXPENSE_E2E_MANAGER_EMPLOYEE_NUMBER,
  EXPENSE_E2E_MANAGER_FIRST_NAME,
  EXPENSE_E2E_MANAGER_PASSWORD,
} from './expenseTestData'

// Runs after 05-expense-creation.spec.ts (numeric filename prefix + workers: 1) so the
// manager's direct report (EMP050) already has Submitted expenses to see here.
test('manager sees a direct report\'s expenses read-only, with no approve/reject controls', async ({
  page,
}) => {
  await registerOrLogin(page, {
    employeeNumber: EXPENSE_E2E_MANAGER_EMPLOYEE_NUMBER,
    email: EXPENSE_E2E_MANAGER_EMAIL,
    password: EXPENSE_E2E_MANAGER_PASSWORD,
    firstName: EXPENSE_E2E_MANAGER_FIRST_NAME,
  })

  await page.goto('/expenses')

  const reportRow = page.getByRole('row', { name: /audrey/i }).first()
  await expect(reportRow).toBeVisible()
  await expect(page.getByRole('button', { name: /approve/i })).toHaveCount(0)
  await expect(page.getByRole('button', { name: /reject/i })).toHaveCount(0)

  await reportRow.getByRole('link').click()

  await expect(page.getByText('This is a read-only view.')).toBeVisible()
  await expect(page.getByRole('link', { name: /edit/i })).toHaveCount(0)
  await expect(page.getByRole('button', { name: /^submit$/i })).toHaveCount(0)
  await expect(page.getByRole('button', { name: /cancel expense/i })).toHaveCount(0)
})
