import { test, expect } from '@playwright/test'
import { E2E_EMAIL, E2E_FIRST_NAME, E2E_PASSWORD } from './testData'

// Runs after 01-auth-registration.spec.ts (numeric filename prefix + playwright.config.ts's
// workers: 1 / fullyParallel: false ensure spec files execute in this order), so the reserved
// account is guaranteed to exist by the time this test logs in with it.
test('logs in with the reserved account and lands on the dashboard', async ({ page }) => {
  await page.goto('/login')
  await page.getByLabel('Email').fill(E2E_EMAIL)
  await page.getByLabel('Password').fill(E2E_PASSWORD)
  await page.getByRole('button', { name: /log in/i }).click()

  await expect(page).toHaveURL(/\/dashboard/)
  await expect(page.getByText(new RegExp(`welcome, ${E2E_FIRST_NAME}`, 'i'))).toBeVisible()
})
