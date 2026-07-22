import { test, expect } from '@playwright/test'
import { E2E_EMAIL, E2E_PASSWORD } from './testData'

test('unauthenticated visit to /dashboard redirects to /login', async ({ page }) => {
  await page.goto('/dashboard')
  await expect(page).toHaveURL(/\/login/)
})

test('after logging in, /dashboard renders', async ({ page }) => {
  await page.goto('/login')
  await page.getByLabel('Email').fill(E2E_EMAIL)
  await page.getByLabel('Password').fill(E2E_PASSWORD)
  await page.getByRole('button', { name: /log in/i }).click()

  await expect(page).toHaveURL(/\/dashboard/)
  await expect(page.getByText(/welcome/i)).toBeVisible()
})
