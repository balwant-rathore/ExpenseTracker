import path from 'node:path'
import { test, expect, type Page } from '@playwright/test'
import { registerOrLogin } from './authHelpers'
import {
  EXPENSE_E2E_EMAIL,
  EXPENSE_E2E_EMPLOYEE_NUMBER,
  EXPENSE_E2E_FIRST_NAME,
  EXPENSE_E2E_FINANCE_EMAIL,
  EXPENSE_E2E_FINANCE_EMPLOYEE_NUMBER,
  EXPENSE_E2E_FINANCE_FIRST_NAME,
  EXPENSE_E2E_FINANCE_PASSWORD,
  EXPENSE_E2E_MANAGER_EMAIL,
  EXPENSE_E2E_MANAGER_EMPLOYEE_NUMBER,
  EXPENSE_E2E_MANAGER_FIRST_NAME,
  EXPENSE_E2E_MANAGER_PASSWORD,
  EXPENSE_E2E_PASSWORD,
} from './expenseTestData'

const RECEIPT_PATH = path.join(__dirname, 'fixtures', 'receipt.pdf')

// ET019 gap-fill (ET020): covers the three ET019 frontend surfaces 01-07 never exercised -
// role-scoped dashboard metrics, the Finance monthly-reimbursement-report download, and the
// receipt/attachment viewer. Reuses the same vetted EXPENSE_E2E_* accounts as 05/06/07 (see
// design.md - dashboard assertions only check tile presence/absence, not exact counts, so
// isolated fresh accounts aren't needed).
test.describe.serial('dashboard metrics, monthly report download, and receipt viewer (ET019 gap-fill)', () => {
  let page: Page
  let expenseUrl: string

  test.beforeAll(async ({ browser }) => {
    page = await browser.newPage()
  })

  test.afterAll(async () => {
    await page.close()
  })

  test('employee sees only employee-scoped dashboard metrics', async () => {
    await registerOrLogin(page, {
      employeeNumber: EXPENSE_E2E_EMPLOYEE_NUMBER,
      email: EXPENSE_E2E_EMAIL,
      password: EXPENSE_E2E_PASSWORD,
      firstName: EXPENSE_E2E_FIRST_NAME,
    })

    await page.goto('/dashboard')

    await expect(page.getByText('Total Submitted')).toBeVisible()
    await expect(page.getByText('Approved')).toBeVisible()
    await expect(page.getByText('Reimbursed')).toBeVisible()
    await expect(page.getByText('Pending Approvals')).toHaveCount(0)
    await expect(page.getByText('Pending Reimbursements')).toHaveCount(0)
  })

  test('manager sees manager-scoped dashboard metrics, including Pending Approvals', async () => {
    await registerOrLogin(page, {
      employeeNumber: EXPENSE_E2E_MANAGER_EMPLOYEE_NUMBER,
      email: EXPENSE_E2E_MANAGER_EMAIL,
      password: EXPENSE_E2E_MANAGER_PASSWORD,
      firstName: EXPENSE_E2E_MANAGER_FIRST_NAME,
    })

    await page.goto('/dashboard')

    await expect(page.getByText('Total Submitted')).toBeVisible()
    await expect(page.getByText('Approved')).toBeVisible()
    await expect(page.getByText('Reimbursed')).toBeVisible()
    await expect(page.getByText('Pending Approvals')).toBeVisible()
    await expect(page.getByText('Pending Reimbursements')).toHaveCount(0)
  })

  test('finance sees finance-scoped dashboard metrics, including Pending Reimbursements', async () => {
    await registerOrLogin(page, {
      employeeNumber: EXPENSE_E2E_FINANCE_EMPLOYEE_NUMBER,
      email: EXPENSE_E2E_FINANCE_EMAIL,
      password: EXPENSE_E2E_FINANCE_PASSWORD,
      firstName: EXPENSE_E2E_FINANCE_FIRST_NAME,
    })

    await page.goto('/dashboard')

    await expect(page.getByText('Total Submitted')).toBeVisible()
    await expect(page.getByText('Approved')).toBeVisible()
    await expect(page.getByText('Reimbursed')).toBeVisible()
    await expect(page.getByText('Pending Approvals')).toBeVisible()
    await expect(page.getByText('Pending Reimbursements')).toBeVisible()
  })

  test('employee submits an expense with a receipt attachment', async () => {
    await registerOrLogin(page, {
      employeeNumber: EXPENSE_E2E_EMPLOYEE_NUMBER,
      email: EXPENSE_E2E_EMAIL,
      password: EXPENSE_E2E_PASSWORD,
      firstName: EXPENSE_E2E_FIRST_NAME,
    })

    await page.goto('/expenses/new')
    await page.getByLabel('Expense date').fill('2026-01-15')
    await page.getByLabel('Category').click()
    await page.getByRole('option', { name: 'Travel' }).click()
    await page.getByLabel('Amount').fill('75')
    await page.getByLabel('Description').fill('E2E receipt viewer fixture expense')
    await page.getByLabel('Receipt attachment').setInputFiles(RECEIPT_PATH)
    await page.getByRole('button', { name: /^submit$/i }).click()

    await expect(page).toHaveURL(/\/expenses\/[^/]+$/)
    await expect(page.getByText('Submitted')).toBeVisible()
    expenseUrl = page.url()
  })

  test('the expense owner can view the receipt attachment', async () => {
    await page.goto(expenseUrl)

    // A new tab opening is the observable proof that the attachment fetch succeeded and
    // openBlobInNewTab's window.open() call fired - the receipt is a real PDF opened via a
    // blob: URL, but Chromium's built-in PDF viewer handles blob: URLs opened into a fresh
    // automated profile inconsistently (sometimes auto-downloads and closes the tab before its
    // URL can be read), so this asserts the tab opens rather than asserting its exact URL/content.
    const [receiptTab] = await Promise.all([
      page.context().waitForEvent('page'),
      page.getByRole('button', { name: /view receipt/i }).click(),
    ])

    expect(receiptTab).toBeTruthy()
    await receiptTab.close().catch(() => {})
  })

  test('finance downloads the monthly reimbursement report', async () => {
    await registerOrLogin(page, {
      employeeNumber: EXPENSE_E2E_FINANCE_EMPLOYEE_NUMBER,
      email: EXPENSE_E2E_FINANCE_EMAIL,
      password: EXPENSE_E2E_FINANCE_PASSWORD,
      firstName: EXPENSE_E2E_FINANCE_FIRST_NAME,
    })

    await page.goto('/reports/monthly-reimbursement')

    const now = new Date()
    const monthName = now.toLocaleString('en-US', { month: 'long' })
    const yearValue = String(now.getFullYear())

    await page.getByRole('combobox', { name: /month/i }).click()
    await page.getByRole('option', { name: monthName }).click()
    await page.getByRole('combobox', { name: /year/i }).click()
    await page.getByRole('option', { name: yearValue }).click()

    const downloadPromise = page.waitForEvent('download')
    await page.getByRole('button', { name: 'Download' }).click()
    const download = await downloadPromise

    expect(download.suggestedFilename()).toMatch(/^Monthly-Reimbursement-\d{4}-\d{2}\.xlsx$/)
  })

  test('a non-Finance role cannot reach the monthly report route', async () => {
    await registerOrLogin(page, {
      employeeNumber: EXPENSE_E2E_EMPLOYEE_NUMBER,
      email: EXPENSE_E2E_EMAIL,
      password: EXPENSE_E2E_PASSWORD,
      firstName: EXPENSE_E2E_FIRST_NAME,
    })

    await page.goto('/reports/monthly-reimbursement')
    await expect(page).not.toHaveURL(/\/reports\/monthly-reimbursement/)
  })
})
