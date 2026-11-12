/**
 * Flow: buying a product bundle.
 *
 * A signed-in visitor opens the bundle from the products page, chooses a billing cycle for each
 * component, searches a domain and picks it, and continues. The checkout then holds the bundle's
 * lines with the discount taken off, and the order the backend receives carries the bundle's id
 * beside the lines (the components at the chosen cycles and the domain). The bundle is 10% off
 * hosting, a mailbox and one year of a domain, so the summary is checked against those prices.
 */
import { expect, test } from './support/test'
import { BUNDLE, FREE_TERM, MAILBOX_PLAN, PLAN, DOMAIN_PRODUCT } from './support/fixtures.mjs'
import { resetStub, signIn, stubRequests, waitForHydration } from './support/helpers'

/** The domain the visitor finds and picks: the searched name under the first offered extension. */
const DOMAIN_NAME = `${FREE_TERM}.com`

test.beforeEach(async ({ context, request, baseURL }) => {
  await resetStub(request)
  await signIn(context, baseURL ?? 'http://127.0.0.1:3100')
})

test('a visitor configures a bundle and the order carries the bundle with its lines', async ({ page, request }) => {
  await page.goto('/products')
  await waitForHydration(page)

  // Products page -> the bundle's order screen.
  await expect(page.getByTestId(`bundle-card-${BUNDLE.id}`)).toContainText(BUNDLE.name)
  await expect(page.getByTestId(`bundle-discount-${BUNDLE.id}`)).toBeVisible()
  await page.getByTestId(`bundle-choose-${BUNDLE.id}`).click()
  await expect(page).toHaveURL(new RegExp(`/bundles/${BUNDLE.id}$`))
  await waitForHydration(page)

  // Nothing can be continued with until the domain is picked.
  await expect(page.getByTestId('bundle-continue')).toBeDisabled()

  // A yearly cycle for each component.
  await page.getByTestId(`bundle-cycle-${PLAN.id}`).click()
  await page.getByTestId('ui-select-option-annual').click()
  await page.getByTestId(`bundle-cycle-${MAILBOX_PLAN.id}`).click()
  await page.getByTestId('ui-select-option-annual').click()

  // Search the domain and pick the available result.
  await page.getByTestId('bundle-domain-input').fill(FREE_TERM)
  await page.getByTestId('bundle-domain-search').click()
  await page.getByTestId(`bundle-domain-pick-${DOMAIN_NAME}`).click()
  await expect(page.getByTestId('bundle-domain-selected')).toContainText(DOMAIN_NAME)

  // Hosting $48 + mailbox $30 + domain $10 = $88, less 10%: $8.80 off, $79.20 to pay.
  await expect(page.getByTestId('bundle-line')).toHaveCount(3)
  await expect(page.getByTestId('bundle-summary-amount')).toContainText('8.80')
  await expect(page.getByTestId('bundle-total')).toContainText('79.20')
  await expect(page.getByTestId('bundle-continue')).toBeEnabled()

  // Continue -> the checkout holds every line and the discount.
  await page.getByTestId('bundle-continue').click()
  await expect(page).toHaveURL(/\/checkout$/)
  await expect(page.getByTestId(`checkout-item-${PLAN.id}`)).toBeVisible()
  await expect(page.getByTestId(`checkout-item-${MAILBOX_PLAN.id}`)).toBeVisible()
  await expect(page.getByTestId(`checkout-item-${DOMAIN_PRODUCT.id}`)).toContainText(DOMAIN_NAME)
  await expect(page.getByTestId('bundle-summary-discount')).toContainText(BUNDLE.name)
  await expect(page.getByTestId('bundle-summary-amount')).toContainText('8.80')
  await expect(page.getByTestId('checkout-total')).toContainText('79.20')

  // The order names the bundle and carries its lines.
  await page.getByTestId('checkout-place-order').click()
  await expect(page).toHaveURL(/\/client\/invoices\/\d+\/pay$/)

  const order = (await stubRequests(request)).find(r => r.method === 'POST' && r.path === '/api/orders')
  expect(order?.body?.bundleId).toBe(BUNDLE.id)
  expect(order?.body?.items).toEqual([
    expect.objectContaining({ productId: PLAN.id, billingcycle: 'annual', domain: DOMAIN_NAME }),
    expect.objectContaining({ productId: MAILBOX_PLAN.id, billingcycle: 'annual' }),
    expect.objectContaining({ productId: DOMAIN_PRODUCT.id, domain: DOMAIN_NAME, domainAction: 'register' }),
  ])
})
