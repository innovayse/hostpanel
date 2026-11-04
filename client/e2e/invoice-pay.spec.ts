/**
 * Flow: client dashboard -> invoice -> pay page.
 *
 * A signed-in customer opens an unpaid invoice from the dashboard, goes to its pay page and pays
 * with the saved card. Checks the payment really went to the backend for that invoice.
 */
import { expect, test } from './support/test'
import { INVOICE } from './support/fixtures.mjs'
import { resetStub, signIn, stubRequests, waitForHydration } from './support/helpers'

test('a customer pays an unpaid invoice with a saved card', async ({ page, context, request, baseURL }) => {
  await resetStub(request)
  await signIn(context, baseURL ?? 'http://127.0.0.1:3100')

  await page.goto('/client/dashboard')
  await waitForHydration(page)
  await expect(page.getByTestId('dashboard-title')).toBeVisible()

  // Dashboard -> the invoice.
  await page.getByTestId(`dashboard-invoice-${INVOICE.id}`).click()
  await expect(page).toHaveURL(new RegExp(`/client/invoices/${INVOICE.id}$`))
  await expect(page.getByTestId('invoice-heading')).toBeVisible()

  // Invoice -> its pay page.
  await page.getByTestId('invoice-pay-now').click()
  await expect(page).toHaveURL(new RegExp(`/client/invoices/${INVOICE.id}/pay$`))

  // Pay with the preselected saved card.
  await page.getByTestId('invoice-pay-submit').click()
  await expect(page.getByTestId('invoice-pay-success')).toBeVisible()
  await expect(page.getByTestId('invoice-pay-error')).toHaveCount(0)

  const requests = await stubRequests(request)
  const pay = requests.find(r => r.method === 'POST' && r.path === `/api/me/invoices/${INVOICE.id}/pay`)
  expect(pay).toBeDefined()
  // Each pay action carries its own attempt id, the key the backend deduplicates the charge by.
  expect(pay?.body).toEqual({ gatewayModuleId: 'stripe', paymentMethodId: expect.any(String), attemptId: expect.stringMatching(/^[0-9a-f-]{36}$/) })
})
