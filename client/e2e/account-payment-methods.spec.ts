/**
 * Flow: the account's saved payment methods.
 *
 * The tab lists the saved card; making it the default and removing it each reach the backend (the
 * removal only after a confirmation) and the list is read again. A card cannot be added here: the
 * add action opens a separate billing portal that this deployment does not run, so the tab offers
 * no add button. A member of the account without the permission to view and pay invoices finds the
 * tab saying why it offers nothing, and the portal does not ask for the list.
 */
import { expect, test } from './support/test'
import { MEMBER_WITHOUT_PURCHASE_ACCOUNT, SAVED_CARD } from './support/fixtures.mjs'
import { resetStub, setAccount, signIn, stubRequests, waitForHydration } from './support/helpers'

test.beforeEach(async ({ page, context, request, baseURL }) => {
  await resetStub(request)
  await signIn(context, baseURL ?? 'http://127.0.0.1:3100')

  await page.goto('/client/account')
  await waitForHydration(page)
  await page.getByTestId('ui-tab-payment').click()
  await expect(page.getByTestId('account-payment-tab')).toBeVisible()
})

test('the saved card is listed with its last digits and no add button is offered', async ({ page }) => {
  await expect(page.getByTestId(`account-payment-${SAVED_CARD.id}`)).toContainText(SAVED_CARD.last4)
  await expect(page.getByTestId('account-payment-add')).toHaveCount(0)
})

test('making the card the default reaches the backend and confirms', async ({ page, request }) => {
  await page.getByTestId(`account-payment-default-${SAVED_CARD.id}`).click()

  await expect(page.getByTestId('account-payment-success')).toBeVisible()
  const sent = (await stubRequests(request)).find(r => r.method === 'PUT' && r.path === `/api/me/payment-methods/${SAVED_CARD.id}`)
  expect(sent?.body).toMatchObject({ setAsDefault: true })
})

test('removing the card asks first, then removes it and the list empties', async ({ page, request }) => {
  await page.getByTestId(`account-payment-remove-${SAVED_CARD.id}`).click()

  // Nothing is deleted until the dialog is confirmed.
  await expect(page.getByTestId('ui-confirm-dialog')).toBeVisible()
  expect((await stubRequests(request)).some(r => r.method === 'DELETE')).toBe(false)

  await page.getByTestId('ui-confirm-confirm').click()

  await expect(page.getByTestId(`account-payment-${SAVED_CARD.id}`)).toHaveCount(0)
  await expect(page.getByTestId('account-payment-success')).toBeVisible()
  expect((await stubRequests(request)).some(r => r.method === 'DELETE' && r.path === `/api/me/payment-methods/${SAVED_CARD.id}`)).toBe(true)
})

test('a member without the invoices permission is told why the tab is empty and no list is requested', async ({ page, request }) => {
  await setAccount(request, MEMBER_WITHOUT_PURCHASE_ACCOUNT)
  /** Counts the payment method list requests the stub has seen so far. */
  const listReads = async (): Promise<number> =>
    (await stubRequests(request)).filter(r => r.method === 'GET' && r.path === '/api/me/payment-methods').length
  const before = await listReads()

  await page.reload()
  await waitForHydration(page)
  await page.getByTestId('ui-tab-payment').click()

  await expect(page.getByTestId('account-payment-denied')).toBeVisible()
  await expect(page.getByTestId(`account-payment-${SAVED_CARD.id}`)).toHaveCount(0)
  expect(await listReads()).toBe(before)
})
