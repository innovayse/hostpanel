/**
 * Sign-in gate beyond the password form: an SSO deployment offers one button that hands the browser
 * to the API's sign-in redirect, and signing out ends the session and lands on the login page.
 *
 * The API is stubbed at the network layer (`support/stubApi.ts`).
 */
import { HttpMethod } from '../src/types/http'
import { expect, test } from '@playwright/test'
import { adminMe } from './fixtures/auth'
import { json, stubApi } from './support/stubApi'

test('an SSO deployment shows the SSO button instead of the password form and redirects through the API', async ({ page }) => {
  const api = await stubApi(page, {
    routes: [
      json(HttpMethod.Get, '/api/auth/mode', { mode: 'sso' }),
      // The redirect endpoint the button navigates to; the stub stands in for the SSO hand-off.
      json(HttpMethod.Get, '/api/auth/login', {}),
    ],
  })

  await page.goto('/login')

  await expect(page.getByTestId('login-sso')).toBeVisible()
  await expect(page.getByTestId('login-email')).toHaveCount(0)

  await page.getByTestId('login-sso').click()
  await expect.poll(() => api.callsTo(HttpMethod.Get, '/api/auth/login').length).toBe(1)

  // The browser asks to come back to the page it left from.
  const [redirect] = api.callsTo(HttpMethod.Get, '/api/auth/login')
  expect(redirect?.search).toContain('returnTo=')
})

test('signing out ends the session and returns to the login page', async ({ page }) => {
  // Session state the stub holds: the logout call flips it, as the real cookie clearing does.
  let signedIn = true
  const api = await stubApi(page, {
    signedIn: true,
    routes: [
      {
        method: HttpMethod.Get,
        path: '/api/auth/me',
        respond: () => (signedIn
          ? { body: adminMe }
          : { status: 401, body: { type: 'auth.unauthenticated', title: 'Unauthorized', status: 401 } }),
      },
      {
        method: HttpMethod.Delete,
        path: '/api/auth/logout',
        respond: () => {
          signedIn = false
          return { body: { success: true } }
        },
      },
      json(HttpMethod.Get, '/api/admin/dashboard/stats', {
        totalRevenue: 0,
        monthlyRevenue: 0,
        activeServices: 0,
        overdueInvoices: 0,
        openTickets: 0,
        totalClients: 0,
        currency: 'USD',
      }),
    ],
  })

  await page.goto('/dashboard')
  await expect(page.getByTestId('app-topbar')).toBeVisible()

  await page.getByTestId('topbar-account').click()
  await page.getByTestId('topbar-sign-out').click()

  await page.waitForURL('**/login')
  await expect(page.getByTestId('login-email')).toBeVisible()
  expect(api.callsTo(HttpMethod.Delete, '/api/auth/logout')).toHaveLength(1)
})
