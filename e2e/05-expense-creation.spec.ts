import path from 'node:path'
import { test, expect, type Page } from '@playwright/test'
import { registerOrLogin } from './authHelpers'
import {
  EXPENSE_E2E_EMAIL,
  EXPENSE_E2E_EMPLOYEE_NUMBER,
  EXPENSE_E2E_FIRST_NAME,
  EXPENSE_E2E_PASSWORD,
} from './expenseTestData'

const RECEIPT_PATH = path.join(__dirname, 'fixtures', 'receipt.pdf')

// All four tests share one authenticated page/session (test.describe.serial +
// a single page created in beforeAll) so `POST /api/auth/register` (or
// /login, for a repeat run) fires exactly once for this whole file, instead
// of once per test — `RateLimiting:AuthEndpoints` allows only 5 per 5 minutes
// per IP, and calling register at all (even for an already-registered
// account) consumes a slot regardless of outcome.
test.describe.serial('expense creation', () => {
  test.setTimeout(60000)

  let page: Page

  test.beforeAll(async ({ browser }) => {
    page = await browser.newPage()
    await registerOrLogin(page, {
      employeeNumber: EXPENSE_E2E_EMPLOYEE_NUMBER,
      email: EXPENSE_E2E_EMAIL,
      password: EXPENSE_E2E_PASSWORD,
      firstName: EXPENSE_E2E_FIRST_NAME,
    })
  })

  test.afterAll(async () => {
    await page.close()
  })

  test('creates an expense as Draft, then submits it from the detail view', async () => {
    await page.goto('/expenses/new')

    await page.getByLabel('Expense date').fill('2026-01-05')
    await page.getByLabel('Category').click()
    await page.getByRole('option', { name: 'Travel' }).click()
    await page.getByLabel('Amount').fill('150')
    await page.getByLabel('Description').fill('E2E draft-then-submit taxi fare')
    await page.getByLabel('Receipt attachment').setInputFiles(RECEIPT_PATH)

    await page.getByRole('button', { name: /save as draft/i }).click()

    await expect(page).toHaveURL(/\/expenses\/[^/]+$/)
    await expect(page.getByText('Draft')).toBeVisible()

    await page.getByRole('button', { name: /^submit$/i }).click()
    await expect(page.getByText('Submitted')).toBeVisible()
    await expect(page.getByRole('button', { name: /^submit$/i })).toHaveCount(0)
  })

  test('creates an expense with immediate Submit', async () => {
    await page.goto('/expenses/new')

    await page.getByLabel('Expense date').fill('2026-01-06')
    await page.getByLabel('Category').click()
    await page.getByRole('option', { name: 'Meals' }).click()
    await page.getByLabel('Amount').fill('45')
    await page.getByLabel('Description').fill('E2E immediate-submit lunch')
    await page.getByLabel('Receipt attachment').setInputFiles(RECEIPT_PATH)

    await page.getByRole('button', { name: /^submit$/i }).click()

    await expect(page).toHaveURL(/\/expenses\/[^/]+$/)
    await expect(page.getByText('Submitted')).toBeVisible()
  })

  test('edits an existing Draft, including replacing the attachment, then submits', async () => {
    await page.goto('/expenses/new')
    await page.getByLabel('Expense date').fill('2026-01-07')
    await page.getByLabel('Category').click()
    await page.getByRole('option', { name: 'Hotel' }).click()
    await page.getByLabel('Amount').fill('300')
    await page.getByLabel('Description').fill('E2E original description')
    await page.getByLabel('Receipt attachment').setInputFiles(RECEIPT_PATH)
    await page.getByRole('button', { name: /save as draft/i }).click()
    await expect(page.getByText('Draft')).toBeVisible()

    await page.getByRole('link', { name: /edit/i }).click()
    await expect(page.getByLabel('Description')).toHaveValue('E2E original description')
    await page.getByLabel('Description').fill('E2E updated description')
    await page.getByLabel('Receipt attachment').setInputFiles(RECEIPT_PATH)
    await page.getByRole('button', { name: /save changes/i }).click()

    await expect(page).toHaveURL(/\/expenses\/[^/]+$/)
    await expect(page.getByText('E2E updated description')).toBeVisible()

    await page.getByRole('button', { name: /^submit$/i }).click()
    await expect(page.getByText('Submitted')).toBeVisible()
  })

  test('cancels a Submitted expense', async () => {
    await page.goto('/expenses/new')
    await page.getByLabel('Expense date').fill('2026-01-08')
    await page.getByLabel('Category').click()
    await page.getByRole('option', { name: 'Other' }).click()
    await page.getByLabel('Amount').fill('20')
    await page.getByLabel('Description').fill('E2E to-be-cancelled expense')
    await page.getByLabel('Receipt attachment').setInputFiles(RECEIPT_PATH)
    await page.getByRole('button', { name: /^submit$/i }).click()
    await expect(page.getByText('Submitted')).toBeVisible()

    await page.getByRole('button', { name: /cancel expense/i }).click()
    await expect(page.getByText('Cancel this expense?')).toBeVisible()
    await page.getByRole('button', { name: /yes, cancel it/i }).click()

    await expect(page.getByText('Cancelled')).toBeVisible()
    await expect(page.getByRole('button', { name: /cancel expense/i })).toHaveCount(0)
  })
})
