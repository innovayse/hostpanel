/**
 * Flow: saving the payment method for automatic renewals within the checkout payment.
 *
 * The box is offered to a signed-in client paying through a gateway that can store credentials,
 * off by default, and never for a gateway without that capability, for bank transfer or for a guest.
 * Ticking it adds the choice and the consent wording version to the payment start request, so the
 * payer enters the card once, at the gateway: no second save session is opened, neither at checkout
 * nor when the payer returns from the bank.
 */
import type { Page } from '@playwright/test'
import { expect, test } from './support/test'
import { GATEWAY_PAGE_URL, HOSTED_GATEWAY, ORDER, PLAIN_GATEWAY, PLAN } from './support/fixtures.mjs'
import { enableHostedGateway, resetStub, signIn, stubRequests, waitForHydration } from './support/helpers'
import { ConsentTextVersion } from '../types/consenttextversion'
import { HttpMethod } from '../types/httpmethod'

/** localStorage key the cart is persisted under (`stores/cart.ts`). */
const CART_KEY = 'innovayse_cart'

/** Path of the stub route that starts the gateway payment of the placed order. */
const START_PATH = `/api/orders/${ORDER.orderId}/gateway-payment/start`

/** Path of the stub route that opens a stored-method save session. */
const SESSIONS_PATH = '/api/me/stored-payment-methods/sessions'

test.beforeEach(async ({ context, request, baseURL }) => {
  await resetStub(request)
  await enableHostedGateway(request, true)
  await signIn(context, baseURL ?? 'http://127.0.0.1:3100')
})

/**
 * Fills the cart with the plan, as a customer who has just chosen it.
 *
 * @param page The page to prepare.
 * @returns Resolves once the init script is registered.
 */
const fillCart = async (page: Page): Promise<void> => {
  await page.addInitScript(([key, item]) => {
    window.localStorage.setItem(key as string, JSON.stringify([item]))
  }, [CART_KEY, {
    pid: PLAN.id,
    name: PLAN.name,
    billingcycle: 'monthly',
    cycleLabel: 'Monthly',
    amount: 5,
    currency: 'USD',
  }])
}

/**
 * Opens the checkout with a filled cart.
 *
 * @param page The page to drive.
 * @returns Resolves once the page is interactive.
 */
const openCheckout = async (page: Page): Promise<void> => {
  await fillCart(page)
  await page.goto('/checkout')
  await waitForHydration(page)
}

/**
 * Counts the save sessions the stub was asked to open.
 *
 * @param request Playwright's API request context.
 * @returns The number of session requests.
 */
const sessionRequests = async (request: Parameters<typeof stubRequests>[0]): Promise<number> =>
  (await stubRequests(request)).filter(r => r.method === HttpMethod.Post && r.path === SESSIONS_PATH).length

test('the box is off by default and is offered only for a gateway that can store credentials', async ({ page }) => {
  await openCheckout(page)

  await expect(page.getByTestId('checkout-save-for-renewals')).toHaveCount(0)

  await page.getByTestId(`checkout-method-${HOSTED_GATEWAY.module}`).click()

  await expect(page.getByTestId('checkout-save-for-renewals')).toBeVisible()
  await expect(page.getByTestId('checkout-save-for-renewals')).toHaveAttribute('aria-checked', 'false')

  await page.getByTestId(`checkout-method-${PLAIN_GATEWAY.module}`).click()

  await expect(page.getByTestId('checkout-save-for-renewals')).toHaveCount(0)
})

test('a guest is offered no box', async ({ context, page }) => {
  await context.clearCookies()
  await openCheckout(page)

  await page.getByTestId(`checkout-method-${HOSTED_GATEWAY.module}`).click()

  await expect(page.getByTestId('checkout-save-for-renewals')).toHaveCount(0)
})

test('bank transfer offers no box', async ({ page }) => {
  await openCheckout(page)

  await page.getByTestId('checkout-method-bank_transfer').click()

  await expect(page.getByTestId('checkout-save-for-renewals')).toHaveCount(0)
})

test('a ticked box sends the choice and the consent version with the payment start, and opens no second session', async ({ page, request }) => {
  await openCheckout(page)

  await page.getByTestId(`checkout-method-${HOSTED_GATEWAY.module}`).click()
  await page.getByTestId('checkout-save-for-renewals').click()
  const leaving = page.waitForRequest(r => r.url().startsWith(GATEWAY_PAGE_URL))
  await page.getByTestId('checkout-place-order').click()
  await leaving

  const sent = await stubRequests(request)
  const start = sent.find(r => r.method === HttpMethod.Post && r.path === START_PATH)
  expect(start?.body).toMatchObject({
    module: HOSTED_GATEWAY.module,
    savePaymentMethod: true,
    consentTextVersion: ConsentTextVersion.CheckoutAgreement,
  })
  expect(await sessionRequests(request)).toBe(0)
})

test('an unticked box pays without the save choice', async ({ page, request }) => {
  await openCheckout(page)

  await page.getByTestId(`checkout-method-${HOSTED_GATEWAY.module}`).click()
  const leaving = page.waitForRequest(r => r.url().startsWith(GATEWAY_PAGE_URL))
  await page.getByTestId('checkout-place-order').click()
  await leaving

  const start = (await stubRequests(request)).find(r => r.method === HttpMethod.Post && r.path === START_PATH)
  expect(start?.body).toMatchObject({ module: HOSTED_GATEWAY.module, savePaymentMethod: false })
  expect(start?.body).not.toHaveProperty('consentTextVersion')
})

test('the choice is dropped when the payer switches to a gateway that cannot store credentials', async ({ page, request }) => {
  await openCheckout(page)

  await page.getByTestId(`checkout-method-${HOSTED_GATEWAY.module}`).click()
  await page.getByTestId('checkout-save-for-renewals').click()
  await page.getByTestId(`checkout-method-${PLAIN_GATEWAY.module}`).click()
  const leaving = page.waitForRequest(r => r.url().startsWith(GATEWAY_PAGE_URL))
  await page.getByTestId('checkout-place-order').click()
  await leaving

  const start = (await stubRequests(request)).find(r => r.method === HttpMethod.Post && r.path === START_PATH)
  expect(start?.body).toMatchObject({ module: PLAIN_GATEWAY.module, savePaymentMethod: false })
})

test('the return from the bank opens no save session', async ({ page, request }) => {
  await page.goto('/')
  await waitForHydration(page)
  await page.evaluate(([tokenKey, invoiceId]) => {
    window.sessionStorage.setItem(tokenKey as string, JSON.stringify({ token: 'tok-e2e', invoiceId, expiresAt: Date.now() + 60 * 60 * 1000 }))
  }, [`order-payment-token:${ORDER.orderId}`, ORDER.invoiceId])

  await page.goto(`/payment/result?order=${ORDER.orderId}`)
  await waitForHydration(page)

  await expect(page.getByTestId('payment-continue')).toBeVisible()
  expect(await sessionRequests(request)).toBe(0)
})
