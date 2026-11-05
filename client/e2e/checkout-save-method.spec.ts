/**
 * Flow: saving the payment method for automatic renewals at checkout.
 *
 * The box is offered to a signed-in client paying through a hosted gateway, off by default, and
 * never for bank transfer. Ticking it changes nothing about the payment itself: the choice is only
 * kept across the redirect. When the payer returns with a confirmed payment, a stored-method session
 * is started once; a declined payment starts nothing, a reload cannot start a second session, and a
 * session the gateway cannot support leaves the paid order paid with a notice and a link to the
 * saved payment methods.
 */
import type { Page } from '@playwright/test'
import { expect, test } from './support/test'
import { CLIENT, GATEWAY_PAGE_URL, HOSTED_GATEWAY, ORDER, ORDER_DECLINED_ID, PLAN } from './support/fixtures.mjs'
import { enableHostedGateway, resetStub, scriptStub, signIn, stubRequests, waitForHydration } from './support/helpers'
import { ConsentTextVersion } from '../types/consenttextversion'
import { HttpMethod } from '../types/httpmethod'

/** localStorage key the cart is persisted under (`stores/cart.ts`). */
const CART_KEY = 'innovayse_cart'

/** Prefix of the sessionStorage key that holds the renewal-save choice of one order. */
const SAVE_CHOICE_PREFIX = 'checkoutSaveForRenewals:'

/** Prefix of the sessionStorage key that holds the payment token of one order. */
const PAYMENT_TOKEN_PREFIX = 'order-payment-token:'

/** Path of the stub route that opens a stored-method save session. */
const SESSIONS_PATH = '/api/me/stored-payment-methods/sessions'

test.beforeEach(async ({ context, request, baseURL }) => {
  await resetStub(request)
  await enableHostedGateway(request)
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
 * Leaves the browser as it is when the payer comes back from the bank: the order's payment token is
 * remembered, and so is the renewal-save choice (owned by the signed-in account) when the payer made it. Done on a page of the site,
 * outside an init script, so a reload of the return page keeps what the page itself spent.
 *
 * @param page The page to prepare.
 * @param orderId The order the payer paid.
 * @param chosen Whether the payer ticked the renewal-save box.
 * @returns Resolves once the storage is filled.
 */
const arriveFromBank = async (page: Page, orderId: number, chosen: boolean): Promise<void> => {
  await page.goto('/')
  await waitForHydration(page)
  await page.evaluate(([tokenKey, saveKey, module, withChoice, invoiceId, owner]) => {
    window.sessionStorage.setItem(tokenKey as string, JSON.stringify({ token: 'tok-e2e', invoiceId, expiresAt: Date.now() + 60 * 60 * 1000 }))
    if (withChoice) {
      window.sessionStorage.setItem(saveKey as string, JSON.stringify({ gatewayModuleId: module, owner, expiresAt: Date.now() + 60 * 60 * 1000 }))
    }
  }, [`${PAYMENT_TOKEN_PREFIX}${orderId}`, `${SAVE_CHOICE_PREFIX}${orderId}`, HOSTED_GATEWAY.module, chosen, ORDER.invoiceId, CLIENT.email.toLowerCase()])
  await page.goto(`/payment/result?order=${orderId}`)
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

test('the box is off by default and is offered only for a hosted gateway', async ({ page }) => {
  await fillCart(page)
  await page.goto('/checkout')
  await waitForHydration(page)

  await expect(page.getByTestId('checkout-save-for-renewals')).toHaveCount(0)

  await page.getByTestId(`checkout-method-${HOSTED_GATEWAY.module}`).click()

  await expect(page.getByTestId('checkout-save-for-renewals')).toBeVisible()
  await expect(page.getByTestId('checkout-save-for-renewals')).toHaveAttribute('aria-checked', 'false')
})

test('a ticked box pays exactly as before and only keeps the choice for the return', async ({ page, request }) => {
  await fillCart(page)
  await page.goto('/checkout')
  await waitForHydration(page)

  await page.getByTestId(`checkout-method-${HOSTED_GATEWAY.module}`).click()
  await page.getByTestId('checkout-save-for-renewals').click()
  const leaving = page.waitForRequest(r => r.url().startsWith(GATEWAY_PAGE_URL))
  await page.getByTestId('checkout-place-order').click()
  await leaving
  // The bank's page is another origin (and blocked here), whose document cannot be asked for its
  // storage; the payer's tab keeps it for the site, so look at it from a page of the site.
  await page.goto('/')
  await waitForHydration(page)

  const sent = await stubRequests(request)
  expect(sent.some(r => r.method === HttpMethod.Post && r.path === `/api/orders/${ORDER.orderId}/gateway-payment/start`)).toBe(true)
  expect(await sessionRequests(request)).toBe(0)
  const kept = await page.evaluate(key => window.sessionStorage.getItem(key), `${SAVE_CHOICE_PREFIX}${ORDER.orderId}`)
  expect(kept).toContain(HOSTED_GATEWAY.module)
})

test('a confirmed payment starts one save session, even when the page is reloaded', async ({ page, request }) => {
  await arriveFromBank(page, ORDER.orderId, true)

  // A started session hands the browser on to the gateway's page at once, so the result page itself is
  // not asserted here: the request is the proof.
  await expect.poll(() => sessionRequests(request)).toBe(1)
  const session = (await stubRequests(request)).find(r => r.method === HttpMethod.Post && r.path === SESSIONS_PATH)
  expect(session?.body).toMatchObject({ gatewayModuleId: HOSTED_GATEWAY.module, consentAccepted: true,
    consentTextVersion: ConsentTextVersion.CheckoutAgreement
  })

  // The payer comes back to the same address again (a reload of it): the choice was spent, so nothing starts.
  await page.goto(`/payment/result?order=${ORDER.orderId}`)
  await waitForHydration(page)

  await expect(page.getByTestId('payment-title')).toBeVisible()
  expect(await sessionRequests(request)).toBe(1)
})

test('a payment without the choice starts no save session', async ({ page, request }) => {
  await arriveFromBank(page, ORDER.orderId, false)

  await expect(page.getByTestId('payment-continue')).toBeVisible()
  expect(await sessionRequests(request)).toBe(0)
})

test('a declined payment starts no save session', async ({ page, request }) => {
  await arriveFromBank(page, ORDER_DECLINED_ID, true)

  await expect(page.getByTestId('payment-try-again')).toBeVisible()
  expect(await sessionRequests(request)).toBe(0)
})

test('a gateway that cannot store credentials leaves the paid order paid, with a notice and a link to the saved methods', async ({ page, request }) => {
  await scriptStub(request, { method: HttpMethod.Post, path: SESSIONS_PATH, status: 409, code: 'STORED_CREDENTIALS_NOT_SUPPORTED', detail: 'E2E: not supported.' })

  await arriveFromBank(page, ORDER.orderId, true)

  await expect(page.getByTestId('payment-save-failed')).toBeVisible()
  await expect(page.getByTestId('payment-continue')).toBeVisible()
  await page.getByTestId('payment-save-failed-link').click()
  await expect(page).toHaveURL(/\/client\/account\?tab=payment/)
})
