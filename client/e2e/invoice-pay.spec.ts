/**
 * Flow: paying an invoice through a hosted gateway.
 *
 * With a gateway enabled the pay page offers one button that starts the payment and hands the
 * browser to the gateway's page; with none it says online payment is unavailable and shows no
 * button and no security notice.
 */
import { expect, test } from './support/test'
import { GATEWAY_PAGE_URL, HOSTED_GATEWAY, INVOICE } from './support/fixtures.mjs'
import { enableHostedGateway, resetStub, signIn, stubRequests, waitForHydration } from './support/helpers'

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

test('without a gateway the page says online payment is unavailable and offers no button or notice', async ({ page }) => {
  await page.goto(`/client/invoices/${INVOICE.id}/pay`)
  await waitForHydration(page)

  await expect(page.getByTestId('invoice-pay-unavailable')).toBeVisible()
  await expect(page.getByTestId('invoice-pay-hosted')).toHaveCount(0)
  await expect(page.getByTestId('invoice-pay-security')).toHaveCount(0)
  await expect(page.getByTestId('invoice-pay-options-error')).toHaveCount(0)
})
