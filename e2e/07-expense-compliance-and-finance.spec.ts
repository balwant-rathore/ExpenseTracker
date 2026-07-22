import path from 'node:path'
import { test, expect, type Page } from '@playwright/test'
import { registerOrLogin } from './authHelpers'
import {
  EXPENSE_E2E_COMPLIANCE_EMAIL,
  EXPENSE_E2E_COMPLIANCE_EMPLOYEE_NUMBER,
  EXPENSE_E2E_COMPLIANCE_FIRST_NAME,
  EXPENSE_E2E_COMPLIANCE_PASSWORD,
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

// Exercises the full ClientEntertainment workflow ET018 adds a UI for:
// Employee submits -> Manager approves -> Compliance approves -> Finance reimburses,
// then confirms Finance search can find it and non-Finance roles can't reach the search route.
// One shared page, switching identity per step via registerOrLogin, since each step's actor is
// a different role reviewing the same expense in sequence.
test.describe.serial('client entertainment review workflow (ET018)', () => {
  let page: Page
  let expenseUrl: string

  test.beforeAll(async ({ browser }) => {
    page = await browser.newPage()
  })

  test.afterAll(async () => {
    await page.close()
  })

  test('employee submits a Client Entertainment expense', async () => {
    await registerOrLogin(page, {
      employeeNumber: EXPENSE_E2E_EMPLOYEE_NUMBER,
      email: EXPENSE_E2E_EMAIL,
      password: EXPENSE_E2E_PASSWORD,
      firstName: EXPENSE_E2E_FIRST_NAME,
    })

    await page.goto('/expenses/new')
    await page.getByLabel('Expense date').fill('2026-01-10')
    await page.getByLabel('Category').click()
    await page.getByRole('option', { name: 'Client Entertainment' }).click()
    await page.getByLabel('Amount').fill('500')
    await page.getByLabel('Description').fill('E2E client entertainment dinner')
    await page.getByLabel('Receipt attachment').setInputFiles(RECEIPT_PATH)
    await page.getByRole('button', { name: /^submit$/i }).click()

    await expect(page).toHaveURL(/\/expenses\/[^/]+$/)
    await expect(page.getByText('Submitted')).toBeVisible()
    expenseUrl = page.url()
  })

  test('manager approves it', async () => {
    await registerOrLogin(page, {
      employeeNumber: EXPENSE_E2E_MANAGER_EMPLOYEE_NUMBER,
      email: EXPENSE_E2E_MANAGER_EMAIL,
      password: EXPENSE_E2E_MANAGER_PASSWORD,
      firstName: EXPENSE_E2E_MANAGER_FIRST_NAME,
    })

    await page.goto(expenseUrl)
    await expect(page.getByRole('button', { name: /^approve$/i })).toBeVisible()
    await page.getByRole('button', { name: /^approve$/i }).click()
    await expect(page.getByText('Approved', { exact: true })).toBeVisible()
  })

  test('compliance officer approves the Client Entertainment expense', async () => {
    await registerOrLogin(page, {
      employeeNumber: EXPENSE_E2E_COMPLIANCE_EMPLOYEE_NUMBER,
      email: EXPENSE_E2E_COMPLIANCE_EMAIL,
      password: EXPENSE_E2E_COMPLIANCE_PASSWORD,
      firstName: EXPENSE_E2E_COMPLIANCE_FIRST_NAME,
    })

    await page.goto(expenseUrl)
    await expect(page.getByRole('button', { name: /^approve$/i })).toBeVisible()
    await page.getByRole('button', { name: /^approve$/i }).click()
    await expect(page.getByText('Compliance Approved')).toBeVisible()
  })

  test('finance reimburses it from the detail screen', async () => {
    await registerOrLogin(page, {
      employeeNumber: EXPENSE_E2E_FINANCE_EMPLOYEE_NUMBER,
      email: EXPENSE_E2E_FINANCE_EMAIL,
      password: EXPENSE_E2E_FINANCE_PASSWORD,
      firstName: EXPENSE_E2E_FINANCE_FIRST_NAME,
    })

    await page.goto(expenseUrl)
    await expect(page.getByRole('button', { name: /reimburse/i })).toBeVisible()
    await page.getByRole('button', { name: /reimburse/i }).click()
    await expect(page.getByText('Reimbursed')).toBeVisible()
    await expect(page.getByRole('button', { name: /reimburse/i })).toHaveCount(0)
  })

  test('finance can find the reimbursed expense via /finance/search', async () => {
    await page.goto('/finance/search')
    await page.getByLabel('Employee name').fill('Audrey')
    await page.getByRole('button', { name: /^search$/i }).click()

    await expect(page.getByRole('link', { name: /E2E client entertainment dinner/i }).or(
      page.getByRole('cell', { name: /audrey/i }),
    ).first()).toBeVisible()
    await expect(page.getByRole('button', { name: /reimburse/i })).toHaveCount(0)
  })

  test('a non-Finance role cannot reach /finance/search', async () => {
    await registerOrLogin(page, {
      employeeNumber: EXPENSE_E2E_EMPLOYEE_NUMBER,
      email: EXPENSE_E2E_EMAIL,
      password: EXPENSE_E2E_PASSWORD,
      firstName: EXPENSE_E2E_FIRST_NAME,
    })

    await page.goto('/finance/search')
    await expect(page).not.toHaveURL(/\/finance\/search/)
  })
})
