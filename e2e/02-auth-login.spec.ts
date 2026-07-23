import { test, expect } from '@playwright/test'
import { E2E_EMAIL, E2E_PASSWORD } from './testData'

// Runs after 01-auth-registration.spec.ts (numeric filename prefix + playwright.config.ts's
// workers: 1 / fullyParallel: false ensure spec files execute in this order), so the reserved
// account is guaranteed to exist by the time this test logs in with it.
test('logs in with the reserved account and lands on the dashboard', async ({ page }) => {
  await page.goto('/login')
  await page.getByLabel('Email').fill(E2E_EMAIL)
  await page.getByLabel('Password').fill(E2E_PASSWORD)
  await page.getByRole('button', { name: /log in/i }).click()

  await expect(page).toHaveURL(/\/dashboard/)
  // ET020 fix: this used to assert a "Welcome, {name}" banner that no longer exists anywhere
  // in the app (RegistrationForm/LoginForm navigate straight to /dashboard) - the Dashboard
  // heading is the real, current signal that login succeeded and the page rendered.
  await expect(page.getByRole('heading', { name: 'Dashboard' })).toBeVisible()
})
