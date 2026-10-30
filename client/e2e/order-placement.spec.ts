/**
 * Flow: placing an order from the checkout.
 *
 * A signed-in customer with a plan in the cart pays by bank transfer: the order reaches the
 * backend with the cart's items, the chosen method and the payer's currency, and the customer is
 * sent to the invoice the backend raised. A refused order keeps the customer on the checkout with
 * the backend's sentence. A member of the account without the permission to place orders sees the
 * payment step disabled with the reason and cannot send the order, while a signed-in first-time
 * buyer, who acts for no account yet, can. Bank transfer is the method used because it needs neither a card
 * element nor a redirect to a bank, so the whole path runs against the stub backend.
 */
import { expect, test } from './support/test'
import { FIRST_TIME_BUYER_ACCOUNT, MEMBER_WITHOUT_PURCHASE_ACCOUNT, ORDER, PLAN } from './support/fixtures.mjs'
import { resetStub, scriptStub, setAccount, signIn, stubRequests, waitForHydration } from './support/helpers'
import { HttpMethod } from '../types/httpmethod'

/** localStorage key the cart is persisted under (`stores/cart.ts`). */
const CART_KEY = 'innovayse_cart'

test.beforeEach(async ({ page, context, request, baseURL }) => {
  await resetStub(request)
  await signIn(context, baseURL ?? 'http://127.0.0.1:3100')
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
})

test('a signed-in customer places an order and lands on its invoice', async ({ page, request }) => {
  await page.goto('/checkout')
  await waitForHydration(page)

  await expect(page.getByTestId('checkout-logged-in')).toBeVisible()
  await expect(page.getByTestId(`checkout-item-${PLAN.id}`)).toBeVisible()
  await expect(page.getByTestId('checkout-method-bank_transfer')).toBeVisible()

  await page.getByTestId('checkout-place-order').click()

  await expect(page).toHaveURL(new RegExp(`/client/invoices/${ORDER.invoiceId}/pay$`))

  const order = (await stubRequests(request)).find(r => r.method === 'POST' && r.path === '/api/orders')
  expect(order?.body).toMatchObject({ paymentmethod: 'bank_transfer', currency: 'USD' })
  expect(order?.body?.items).toEqual([expect.objectContaining({ productId: PLAN.id, billingcycle: 'monthly' })])
})

test('a refused order keeps the customer on the checkout with the backend sentence', async ({ page, request }) => {
  await scriptStub(request, { method: HttpMethod.Post, path: '/api/orders', status: 400, code: 'ORDER_REFUSED', detail: 'E2E: the order was refused.' })

  await page.goto('/checkout')
  await waitForHydration(page)
  await page.getByTestId('checkout-place-order').click()

  await expect(page.getByTestId('checkout-error')).toContainText('E2E: the order was refused.')
  await expect(page).toHaveURL(/\/checkout$/)
  await expect(page.getByTestId('checkout-place-order')).toBeEnabled()
})

test('a member without the permission to place orders sees the payment step disabled with the reason', async ({ page, request }) => {
  await setAccount(request, MEMBER_WITHOUT_PURCHASE_ACCOUNT)

  await page.goto('/checkout')
  await waitForHydration(page)

  await expect(page.getByTestId('checkout-permission-denied')).toBeVisible()
  await expect(page.getByTestId('checkout-method-bank_transfer-input')).toBeDisabled()
  await expect(page.getByTestId('checkout-place-order')).toBeDisabled()
  expect((await stubRequests(request)).some(r => r.method === 'POST' && r.path === '/api/orders')).toBe(false)
})

test('a signed-in first-time buyer, who acts for no account yet, can place the order', async ({ page, request }) => {
  await setAccount(request, FIRST_TIME_BUYER_ACCOUNT)

  await page.goto('/checkout')
  await waitForHydration(page)

  await expect(page.getByTestId('checkout-permission-denied')).toHaveCount(0)
  await expect(page.getByTestId('checkout-place-order')).toBeEnabled()

  await page.getByTestId('checkout-place-order').click()

  await expect(page).toHaveURL(new RegExp(`/client/invoices/${ORDER.invoiceId}/pay$`))
})
