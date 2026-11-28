/**
 * Flow: the client area when the backend cannot give the usual answer.
 *
 * Two different refusals, two different screens: an account that is not a customer account gets
 * the one explanation with a way out (not a red alert per section), and a backend that is down
 * gets an error state instead of an empty list that would read as "you have nothing".
 */
import { expect, test } from './support/test'
import { resetStub, scriptStub, signIn, waitForHydration } from './support/helpers'
import { HttpMethod } from '../types/httpmethod'

test('an account without a customer profile sees the notice instead of an empty list', async ({ page, context, request, baseURL }) => {
  await resetStub(request)
  await scriptStub(request, { method: HttpMethod.Get, path: '/api/me/invoices', status: 404, code: 'CLIENT_PROFILE_NOT_FOUND', detail: 'No customer profile.' })
  await signIn(context, baseURL ?? 'http://127.0.0.1:3100')

  await page.goto('/client/invoices')
  await waitForHydration(page)

  await expect(page.getByTestId('no-profile-notice')).toBeVisible()
  await expect(page.getByTestId('invoices-table')).toHaveCount(0)
})

test('a backend that is down shows an error state, not an empty invoice list', async ({ page, context, request, baseURL }) => {
  await resetStub(request)
  await scriptStub(request, { method: HttpMethod.Get, path: '/api/me/invoices', status: 503, code: 'SERVICE_UNAVAILABLE', detail: 'E2E: the service is unavailable.' })
  await signIn(context, baseURL ?? 'http://127.0.0.1:3100')

  await page.goto('/client/invoices')
  await waitForHydration(page)

  await expect(page.getByTestId('invoices-error')).toBeVisible()
  await expect(page.getByTestId('invoices-table')).toHaveCount(0)
})

test('the invoice list opens an invoice', async ({ page, context, request, baseURL }) => {
  await resetStub(request)
  await signIn(context, baseURL ?? 'http://127.0.0.1:3100')

  await page.goto('/client/invoices')
  await waitForHydration(page)
  await expect(page.getByTestId('invoices-table')).toBeVisible()

  await page.getByTestId('invoice-row-501').click()
  await expect(page).toHaveURL(/\/client\/invoices\/501$/)
  await expect(page.getByTestId('invoice-heading')).toBeVisible()
})
