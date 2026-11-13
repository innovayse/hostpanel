/**
 * Demo mode: the login screen's "Use" button of a published account fills in its
 * credentials, and the banner with the reset note appears once signed in.
 *
 * The API is stubbed at the network layer (`support/stubApi.ts`).
 */
import { HttpMethod } from '../src/types/http'
import { expect, test } from '@playwright/test'
import { fieldOf } from './support/locators'
import { json, stubApi } from './support/stubApi'

/** `GET /api/public/demo` answer for a running demo. */
const demoOn = {
  enabled: true,
  nextResetAt: new Date(Date.now() + 90 * 60_000).toISOString(),
  accounts: [
    { role: 'Admin', email: 'demo-admin@example.test', password: 'demo-pass' },
    { role: 'Client', email: 'demo-client@example.test', password: 'client-pass' },
  ],
}

test('the login screen prefills the published demo Admin account', async ({ page }) => {
  await stubApi(page, { routes: [json(HttpMethod.Get, '/api/public/demo', demoOn)] })

  await page.goto('/login')
  await page.getByTestId('demo-login-use').click()

  await expect(fieldOf(page, 'login-email')).toHaveValue('demo-admin@example.test')
  await expect(fieldOf(page, 'login-password')).toHaveValue('demo-pass')
})

test('the login screen lists every published demo account', async ({ page }) => {
  await stubApi(page, { routes: [json(HttpMethod.Get, '/api/public/demo', demoOn)] })

  await page.goto('/login')

  await expect(page.getByTestId('demo-login-account-Admin')).toContainText('demo-admin@example.test')
  await expect(page.getByTestId('demo-login-account-Client')).toContainText('demo-client@example.test')
  await expect(page.getByTestId('demo-login-client-portal')).toBeVisible()
  await expect(page.getByTestId('demo-login-use')).toHaveCount(1)
})

test('the banner shows the label and the countdown once signed in', async ({ page }) => {
  await stubApi(page, { signedIn: true, routes: [json(HttpMethod.Get, '/api/public/demo', demoOn)] })

  await page.goto('/dashboard')

  await expect(page.getByTestId('demo-banner')).toContainText('Demo mode')
  await expect(page.getByTestId('demo-banner-countdown')).toBeVisible()
})
