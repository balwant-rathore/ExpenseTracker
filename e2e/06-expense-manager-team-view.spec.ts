import path from 'node:path'
import { test, expect, type Page } from '@playwright/test'
import { registerOrLogin } from './authHelpers'
import {
  EXPENSE_E2E_MANAGER_EMAIL,
  EXPENSE_E2E_MANAGER_EMPLOYEE_NUMBER,
  EXPENSE_E2E_MANAGER_FIRST_NAME,
  EXPENSE_E2E_MANAGER_PASSWORD,
} from './expenseTestData'

const RECEIPT_PATH = path.join(__dirname, 'fixtures', 'receipt.pdf')

// Runs after 05-expense-creation.spec.ts (numeric filename prefix + workers: 1) so the
// manager's direct report (EMP050) already has several Submitted expenses to review here.
// Each test targets a specific report expense by its unique expense date — the general list's
// table (ExpenseList.tsx) renders expenseNumber/employee/category/amount/status/date columns
// only, not description, so a description-text row selector never matches.
//
// One shared page/session for the whole file (test.beforeAll, not beforeEach) so
// POST /api/auth/register (or /login) fires exactly once here, matching 05's rate-limit-conscious
// pattern — the auth endpoints share a small PermitLimit budget across the whole E2E run.
test.describe.serial('manager team view and review actions (ET018)', () => {
  let page: Page

  test.beforeAll(async ({ browser }) => {
    page = await browser.newPage()
    await registerOrLogin(page, {
      employeeNumber: EXPENSE_E2E_MANAGER_EMPLOYEE_NUMBER,
      email: EXPENSE_E2E_MANAGER_EMAIL,
      password: EXPENSE_E2E_MANAGER_PASSWORD,
      firstName: EXPENSE_E2E_MANAGER_FIRST_NAME,
    })
  })

  test.afterAll(async () => {
    await page.close()
  })

  test("manager sees Approve/Reject on a direct report's Submitted expense and can approve it", async () => {
    await page.goto('/expenses')
    // Filter to Submitted only — repeated local runs of 05/06 against the same persistent dev
    // DB can leave more than one 2026-01-07 row (an earlier run's already-Approved one plus this
    // run's fresh Submitted one), and only the Submitted one is a valid target here.
    await page.getByLabel('Status').click()
    await page.getByRole('option', { name: 'Submitted' }).click()

    const reportRow = page.getByRole('row', { name: /2026-01-07/ }).first()
    await expect(reportRow).toBeVisible()
    await reportRow.getByRole('link').click()

    await expect(page.getByRole('button', { name: /^approve$/i })).toBeVisible()
    await expect(page.getByRole('button', { name: /^reject$/i })).toBeVisible()
    await expect(page.getByRole('link', { name: /edit/i })).toHaveCount(0)
    await expect(page.getByRole('button', { name: /^submit$/i })).toHaveCount(0)
    await expect(page.getByRole('button', { name: /cancel expense/i })).toHaveCount(0)

    await page.getByRole('button', { name: /^approve$/i }).click()
    await expect(page.getByText('Approved')).toBeVisible()
    await expect(page.getByRole('button', { name: /^approve$/i })).toHaveCount(0)
  })

  test("manager rejects a direct report's Submitted expense with a comment", async () => {
    await page.goto('/expenses')
    await page.getByLabel('Status').click()
    await page.getByRole('option', { name: 'Submitted' }).click()

    const reportRow = page.getByRole('row', { name: /2026-01-06/ }).first()
    await expect(reportRow).toBeVisible()
    await reportRow.getByRole('link').click()

    await page.getByRole('button', { name: /^reject$/i }).click()
    await expect(page.getByRole('button', { name: /confirm rejection/i })).toBeDisabled()

    await page.getByLabel(/rejection comment/i).fill('Missing an itemized receipt')
    await page.getByRole('button', { name: /confirm rejection/i }).click()

    await expect(page.getByText('Rejected')).toBeVisible()
    await expect(page.getByText('Missing an itemized receipt')).toBeVisible()
  })

  test("manager cannot reach a direct report's edit route directly", async () => {
    await page.goto('/expenses')

    // Any direct-report row proves the ownership guard — no need to pin a specific date/status.
    const reportRow = page.getByRole('row', { name: /audrey/i }).first()
    await expect(reportRow).toBeVisible()
    await reportRow.getByRole('link').click()

    await expect(page).toHaveURL(/\/expenses\/[^/]+$/)
    const detailUrl = page.url()
    const editUrl = `${detailUrl}/edit`

    await page.goto(editUrl)

    await expect(page).toHaveURL(detailUrl)
    await expect(page.getByLabel('Description')).toHaveCount(0)
  })

  test('manager does not see review actions on their own expense', async () => {
    await page.goto('/expenses/new')
    await page.getByLabel('Expense date').fill('2026-01-09')
    await page.getByLabel('Category').click()
    await page.getByRole('option', { name: 'Travel' }).click()
    await page.getByLabel('Amount').fill('75')
    await page.getByLabel('Description').fill('E2E manager own expense')
    await page.getByLabel('Receipt attachment').setInputFiles(RECEIPT_PATH)
    await page.getByRole('button', { name: /^submit$/i }).click()

    await expect(page.getByText('Submitted')).toBeVisible()
    await expect(page.getByRole('button', { name: /^approve$/i })).toHaveCount(0)
    await expect(page.getByRole('button', { name: /^reject$/i })).toHaveCount(0)
  })
})
