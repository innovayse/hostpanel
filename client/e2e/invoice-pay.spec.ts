/**
 * Flow: paying an invoice through a hosted gateway or by manual bank transfer.
 *
 * With a gateway enabled the pay page offers a button that starts the payment and hands the browser to the gateway's
 * page. The bank transfer is offered beside it: choosing it moves the invoice to waiting, shows the bank details with copy
 * buttons and the reference, and the invoice page shows the same card after a reload. With neither the page says online
 * payment is unavailable and shows no button and no security notice.
 */
import { expect, test } from './support/test'
import { BANK_TRANSFER_DETAILS, GATEWAY_PAGE_URL, HOSTED_GATEWAY, INVOICE } from './support/fixtures.mjs'
import { disableBankTransfer, enableHostedGateway, resetStub, signIn, stubRequests, waitForHydration } from './support/helpers'

test.beforeEach(async ({ context, request, baseURL }) => {
  await resetStub(request)
  await signIn(context, baseURL ?? 'http://127.0.0.1:3100')
})

test('the gateway button starts the payment and leaves for the gateway page', async ({ page, request }) => {
  await enableHostedGateway(request)
  await page.goto(`/client/invoices/${INVOICE.id}/pay`)
  await waitForHydration(page)

  await expect(page.getByTestId('invoice-pay-security')).toBeVisible()
  await expect(page.getByTestId('invoice-pay-unavailable')).toHaveCount(0)

  const leaving = page.waitForRequest(r => r.url().startsWith(GATEWAY_PAGE_URL))
  await page.getByTestId('invoice-pay-hosted').click()
  await leaving

  const sent = (await stubRequests(request)).find(r => r.method === 'POST' && r.path === `/api/me/invoices/${INVOICE.id}/gateway-payment/start`)
  expect(sent?.body).toMatchObject({ module: HOSTED_GATEWAY.module })
})

test('without a card gateway the page offers the bank transfer and no security notice', async ({ page }) => {
  await page.goto(`/client/invoices/${INVOICE.id}/pay`)
  await waitForHydration(page)

  await expect(page.getByTestId('invoice-pay-bank-transfer')).toBeVisible()
  await expect(page.getByTestId('invoice-pay-hosted')).toHaveCount(0)
  await expect(page.getByTestId('invoice-pay-unavailable')).toHaveCount(0)
  await expect(page.getByTestId('invoice-pay-security')).toHaveCount(0)
  await expect(page.getByTestId('invoice-pay-options-error')).toHaveCount(0)
})

test('with neither a card gateway nor the bank transfer the page says online payment is unavailable', async ({ page, request }) => {
  await disableBankTransfer(request)
  await page.goto(`/client/invoices/${INVOICE.id}/pay`)
  await waitForHydration(page)

  await expect(page.getByTestId('invoice-pay-unavailable')).toBeVisible()
  await expect(page.getByTestId('invoice-pay-bank-transfer')).toHaveCount(0)
  await expect(page.getByTestId('invoice-pay-hosted')).toHaveCount(0)
  await expect(page.getByTestId('invoice-pay-security')).toHaveCount(0)
})

test('choosing the bank transfer shows the bank details, the reference and copy buttons, and the invoice page keeps them', async ({ page, request }) => {
  await page.goto(`/client/invoices/${INVOICE.id}/pay`)
  await waitForHydration(page)

  await page.getByTestId('invoice-pay-bank-transfer').click()

  await expect(page.getByTestId('bank-transfer-card')).toBeVisible()
  await expect(page.getByTestId('bank-transfer-reference')).toHaveText(BANK_TRANSFER_DETAILS.reference)
  await expect(page.getByTestId('bank-transfer-iban')).toHaveText(BANK_TRANSFER_DETAILS.iban)
  await expect(page.getByTestId('bank-transfer-swift')).toHaveText(BANK_TRANSFER_DETAILS.swift)
  await expect(page.getByTestId('bank-transfer-accountName')).toHaveText(BANK_TRANSFER_DETAILS.accountName)
  await expect(page.getByTestId('bank-transfer-copy-iban')).toBeVisible()
  await expect(page.getByTestId('bank-transfer-copy-reference')).toBeVisible()
  await expect(page.getByTestId('bank-transfer-status')).toBeVisible()
  await expect(page.getByTestId('invoice-pay-bank-transfer')).toHaveCount(0)

  const sent = (await stubRequests(request)).filter(r => r.method === 'POST' && r.path === `/api/me/invoices/${INVOICE.id}/bank-transfer`)
  expect(sent).toHaveLength(1)

  await page.goto(`/client/invoices/${INVOICE.id}`)
  await waitForHydration(page)

  await expect(page.getByTestId('bank-transfer-card')).toBeVisible()
  await expect(page.getByTestId('bank-transfer-reference')).toHaveText(BANK_TRANSFER_DETAILS.reference)
  await expect(page.getByTestId('invoice-pay-now')).toHaveCount(0)
})

test('the pay page of an invoice that waits for a transfer shows the details again after a reload', async ({ page }) => {
  await page.goto(`/client/invoices/${INVOICE.id}/pay`)
  await waitForHydration(page)
  await page.getByTestId('invoice-pay-bank-transfer').click()
  await expect(page.getByTestId('bank-transfer-card')).toBeVisible()

  await page.reload()
  await waitForHydration(page)

  await expect(page.getByTestId('bank-transfer-reference')).toHaveText(BANK_TRANSFER_DETAILS.reference)
  await expect(page.getByTestId('invoice-pay-bank-transfer')).toHaveCount(0)
  await expect(page.getByTestId('invoice-pay-security')).toHaveCount(0)
})
