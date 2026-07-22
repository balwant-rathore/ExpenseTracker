import { test, expect } from '@playwright/test'
import { E2E_EMAIL } from './testData'

test('forgot-password shows the generic confirmation and navigates to the reset step', async ({
  page,
}) => {
  await page.goto('/forgot-password')
  await page.getByLabel('Email').fill(E2E_EMAIL)
  await page.getByRole('button', { name: /send reset code/i }).click()

  await expect(
    page.getByText('If an account exists for that email, a one-time code has been issued.'),
  ).toBeVisible()

  await page.getByRole('link', { name: /enter code/i }).click()
  await expect(page).toHaveURL(/\/reset-password/)
})

test('a wrong-but-well-formed OTP surfaces the generic invalid-code message', async ({ page }) => {
  // Scoped to what's verifiable without reading the backend's console-logged OTP (design.md
  // Non-Goals) — this does not assert an actual successful password change.
  await page.goto('/reset-password')
  await page.getByLabel('Email').fill(E2E_EMAIL)
  await page.getByLabel('6-digit code').fill('000000')
  await page.getByLabel('New password', { exact: true }).fill('NewPassword1')
  await page.getByLabel('Confirm new password').fill('NewPassword1')
  await page.getByRole('button', { name: /reset password/i }).click()

  await expect(page.getByText('This one-time code is invalid.')).toBeVisible()
})
