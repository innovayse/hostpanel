/**
 * Flow: the client's credit.
 *
 * The credit page shows the balance in the client's currency, the add funds button and the history. Adding funds holds the amount
 * to the limits the backend reported, raises a top-up invoice for it and sends the client to pay that invoice on the ordinary
 * payment page; a refused amount stays on the page. On an invoice that owes money the client can pay part of it from the balance:
 * the balance and what the invoice owes both go down.
 */
import { expect, test } from './support/test'
import { CREDIT, INVOICE, TOP_UP_INVOICE_ID } from './support/fixtures.mjs'
import { resetStub, signIn, stubRequests, waitForHydration } from './support/helpers'

test.beforeEach(async ({ context, request, baseURL }) => {
  await resetStub(request)
  await signIn(context, baseURL ?? 'http://127.0.0.1:3100')
})

test('the credit page shows the balance, the history and the way to add funds', async ({ page }) => {
  await page.goto('/client/credit')
  await waitForHydration(page)

  await expect(page.getByTestId('credit-balance')).toContainText('20.00')
  await expect(page.getByTestId('credit-entry-type-31')).toContainText('Credit added')
  await expect(page.getByTestId('credit-entry-change-31')).toContainText('+')
  await expect(page.getByTestId('credit-add-funds')).toHaveAttribute('href', '/client/credit/add')
})

test('adding funds holds the amount to the limits, raises the top-up invoice and sends the client to pay it', async ({ page, request }) => {
  await page.goto('/client/credit/add')
  await waitForHydration(page)

  await page.getByTestId('credit-add-amount').fill(String(CREDIT.topUpMaximum + 1))
  await page.getByTestId('credit-add-submit').click()
  await expect(page.getByTestId('credit-add-form')).toContainText('between')
  expect((await stubRequests(request)).filter(r => r.method === 'POST' && r.path === '/api/me/credit/top-ups')).toHaveLength(0)

  await page.getByTestId('credit-add-amount').fill('40')
  await page.getByTestId('credit-add-submit').click()

  await expect(page).toHaveURL(new RegExp(`/client/invoices/${TOP_UP_INVOICE_ID}/pay$`))
  const sent = (await stubRequests(request)).find(r => r.method === 'POST' && r.path === '/api/me/credit/top-ups')
  expect(sent?.body).toEqual({ amount: 40 })
})

test('credit pays part of an invoice: the balance and what the invoice owes go down', async ({ page, request }) => {
  await page.goto(`/client/invoices/${INVOICE.id}`)
  await waitForHydration(page)

  await expect(page.getByTestId('apply-credit-card')).toBeVisible()
  // The invoice owes 5.00 and the balance is 20.00, so the most that can be applied is 5.00.
  await page.getByTestId('apply-credit-amount').fill('3')
  await page.getByTestId('apply-credit-submit').click()

  await expect(page.getByTestId('apply-credit-available')).toContainText('17.00')
  await expect(page.getByTestId('invoice-pay-amount')).toContainText('2.00')
  const sent = (await stubRequests(request)).find(r => r.method === 'POST' && r.path === `/api/me/invoices/${INVOICE.id}/credit`)
  expect(sent?.body).toEqual({ amount: 3 })
})

test('more credit than can be applied is refused before the backend is asked', async ({ page, request }) => {
  await page.goto(`/client/invoices/${INVOICE.id}`)
  await waitForHydration(page)

  await page.getByTestId('apply-credit-amount').fill('50')
  await page.getByTestId('apply-credit-submit').click()

  await expect(page.getByTestId('apply-credit-card')).toContainText('up to')
  expect((await stubRequests(request)).filter(r => r.method === 'POST' && r.path === `/api/me/invoices/${INVOICE.id}/credit`)).toHaveLength(0)
})
