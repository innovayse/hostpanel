/**
 * Flow: a session that dies while the customer is working.
 *
 * The cookies say the customer is signed in, but the backend refuses the next call with 401 and
 * there is no refresh token to renew it. The client throws the session away and sends the
 * customer to the sign-in form with the page the refusal interrupted as `?redirect=`; signing in
 * again returns them to that page. A redirect that points off the site is not followed.
 *
 * The step that expires is a client-side one (opening an invoice from the list): the expiry hook
 * runs in the browser, so a first server-rendered load would not exercise it.
 */
import { expect, test } from './support/test'
import { CLIENT_CREDENTIALS, INVOICE } from './support/fixtures.mjs'
import { resetStub, scriptStub, signIn, waitForHydration } from './support/helpers'
import { HttpMethod } from '../types/httpmethod'

test('a 401 on a client-side step sends the customer to sign in, and signing in returns to the page they were on', async ({ page, context, request, baseURL }) => {
  await resetStub(request)
  await signIn(context, baseURL ?? 'http://127.0.0.1:3100')

  await page.goto('/client/invoices')
  await waitForHydration(page)
  await expect(page.getByTestId('invoices-table')).toBeVisible()

  // The session dies now: the next read of one invoice is refused.
  await scriptStub(request, { method: HttpMethod.Get, path: `/api/me/invoices/${INVOICE.id}`, status: 401, code: 'UNAUTHORIZED', detail: 'E2E: the session has expired.' })
  await page.getByTestId(`invoice-row-${INVOICE.id}`).click()

  // The sign-in page remembers the page the refusal interrupted: the invoice being opened.
  await expect(page).toHaveURL(new RegExp(`/client/login\\?redirect=%2Fclient%2Finvoices%2F${INVOICE.id}$`))
  await waitForHydration(page)
  await expect(page.getByTestId('login-form')).toBeVisible()

  // The backend is well again by the time they sign in: drop the scripted refusal.
  await resetStub(request)

  await page.getByTestId('login-email').fill(CLIENT_CREDENTIALS.email)
  await page.getByTestId('login-password').fill(CLIENT_CREDENTIALS.password)
  await page.getByTestId('login-submit').click()

  // Signing in sends them back there, not to the dashboard.
  await expect(page).toHaveURL(new RegExp(`/client/invoices/${INVOICE.id}$`))
  await expect(page.getByTestId('invoice-heading')).toBeVisible()
})

test('after the session expired the gate keeps the client area closed until signing in again', async ({ page, context, request, baseURL }) => {
  await resetStub(request)
  await signIn(context, baseURL ?? 'http://127.0.0.1:3100')

  await page.goto('/client/invoices')
  await waitForHydration(page)
  await scriptStub(request, { method: HttpMethod.Get, path: `/api/me/invoices/${INVOICE.id}`, status: 401, code: 'UNAUTHORIZED', detail: 'E2E: the session has expired.' })
  await page.getByTestId(`invoice-row-${INVOICE.id}`).click()
  await expect(page).toHaveURL(/\/client\/login/)

  // The expired session is gone: the gate no longer lets the client area open without signing in.
  await page.goto('/client/dashboard')
  await expect(page).toHaveURL(/\/client\/login\?redirect=/)
  await waitForHydration(page)

  await page.getByTestId('login-email').fill(CLIENT_CREDENTIALS.email)
  await page.getByTestId('login-password').fill(CLIENT_CREDENTIALS.password)
  await page.getByTestId('login-submit').click()

  await expect(page).toHaveURL(/\/client\/dashboard$/)
  await expect(page.getByTestId('dashboard-title')).toBeVisible()
})

test('a redirect that points off the site is ignored after signing in', async ({ page, request }) => {
  await resetStub(request)

  await page.goto('/client/login?redirect=https%3A%2F%2Fevil.example%2Fphish')
  await waitForHydration(page)
  await page.getByTestId('login-email').fill(CLIENT_CREDENTIALS.email)
  await page.getByTestId('login-password').fill(CLIENT_CREDENTIALS.password)
  await page.getByTestId('login-submit').click()

  await expect(page).toHaveURL(/\/client\/dashboard$/)
})
