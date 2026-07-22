import type { Page } from '@playwright/test'
import { expect } from '@playwright/test'

interface Credentials {
  employeeNumber: string
  email: string
  password: string
  firstName: string
}

/**
 * Registers the given reserved seed account, or logs in if a prior run already
 * registered it — mirrors 01-auth-registration.spec.ts's idempotent pattern so
 * every expense E2E spec can start from a known-authenticated state regardless
 * of run history against the persistent dev database. Only the first call for
 * a given account hits `POST /api/auth/register`; every later call falls
 * through to a genuine `POST /api/auth/login` instead — a separate rate-limit
 * bucket from register's, so calling this once per test is safe.
 */
export async function registerOrLogin(page: Page, credentials: Credentials) {
  await page.goto('/register')
  await page.getByLabel('Employee number').fill(credentials.employeeNumber)
  await page.getByLabel('Email').fill(credentials.email)
  await page.getByLabel('Password', { exact: true }).fill(credentials.password)
  await page.getByLabel('Confirm password').fill(credentials.password)
  await page.getByRole('button', { name: /create account/i }).click()

  const welcomed = page.getByText(new RegExp(`welcome, ${credentials.firstName}`, 'i'))
  const alreadyRegistered = page.getByText('Registration could not be completed.')
  await expect(welcomed.or(alreadyRegistered)).toBeVisible()

  if (await alreadyRegistered.isVisible().catch(() => false)) {
    await page.goto('/login')
    await page.getByLabel('Email').fill(credentials.email)
    await page.getByLabel('Password').fill(credentials.password)
    await page.getByRole('button', { name: /log in/i }).click()
    await expect(page).toHaveURL(/\/dashboard/)
  }
}
