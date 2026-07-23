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
 *
 * A successful registration navigates straight to `/dashboard` (see
 * `RegistrationForm.tsx`'s `onSuccess: () => navigate('/dashboard')`) — there is no
 * "Welcome, {name}" interstitial anywhere in the app, so which branch happened is
 * detected by whether the app navigates away from `/register` versus the
 * `alreadyRegistered` error text appearing while still on it (ET020 fix — a prior
 * version of this helper waited for non-existent "welcome" text and always timed out
 * for currently-used, already-registered accounts).
 */
export async function registerOrLogin(page: Page, credentials: Credentials) {
  await page.goto('/register')
  await page.getByLabel('Employee number').fill(credentials.employeeNumber)
  await page.getByLabel('Email').fill(credentials.email)
  await page.getByLabel('Password', { exact: true }).fill(credentials.password)
  await page.getByLabel('Confirm password').fill(credentials.password)
  await page.getByRole('button', { name: /create account/i }).click()

  const alreadyRegistered = page.getByText('Registration could not be completed.')
  const navigatedAway = page
    .waitForURL((url) => !url.pathname.startsWith('/register'), { timeout: 5000 })
    .then(() => true)
    .catch(() => false)
  const stayedOnRegisterWithError = alreadyRegistered
    .waitFor({ state: 'visible', timeout: 5000 })
    .then(() => true)
    .catch(() => false)

  const [navigated, rejected] = await Promise.all([navigatedAway, stayedOnRegisterWithError])
  if (!navigated && !rejected) {
    throw new Error(
      'registerOrLogin: registration neither navigated away from /register nor showed the ' +
        '"already registered" error — the app may be in an unexpected state.',
    )
  }

  if (rejected) {
    await page.goto('/login')
    await page.getByLabel('Email').fill(credentials.email)
    await page.getByLabel('Password').fill(credentials.password)
    await page.getByRole('button', { name: /log in/i }).click()
    // Not /dashboard specifically - DashboardPage redirects ComplianceOfficer to /expenses
    // (ET020 fix: this previously hardcoded /dashboard, which broke for that one role).
    await expect(page).not.toHaveURL(/\/login$/)
  }
}
