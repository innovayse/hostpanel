/**
 * Flow: the account's saved payment methods.
 *
 * The tab lists the saved card; making it the default and removing it each reach the backend (the
 * removal only after a confirmation) and the list is read again.
 *
 * "Add card" opens a dialog. The built-in card module shows the payment provider's own card field,
 * which the provider's script is not reachable for in this suite, so the save button stays off; a
 * gateway with its own page shows no card field. A card is never saved without the payer ticking the
 * agreement to automatic renewal charges, and a refusal of the API is shown as its own sentence.
 *
 * A card charged automatically is marked, and one button, after a confirmation, turns automatic charges
 * off while the card stays on file.
 *
 * A member of the account without the permission to view and pay invoices finds the tab saying why it
 * offers nothing, and the portal does not ask for the list.
 */
import type { Page } from '@playwright/test'
import { expect, test } from './support/test'
import { MEMBER_WITHOUT_PURCHASE_ACCOUNT, SAVED_CARD } from './support/fixtures.mjs'
import { resetStub, scriptStub, setAccount, signIn, stubRequests, waitForHydration } from './support/helpers'
import { HttpMethod } from '../types/httpmethod'

test.beforeEach(async ({ page, context, request, baseURL }) => {
  await resetStub(request)
  await signIn(context, baseURL ?? 'http://127.0.0.1:3100')

  await page.goto('/client/account')
  await waitForHydration(page)
  await page.getByTestId('ui-tab-payment').click()
  await expect(page.getByTestId('account-payment-tab')).toBeVisible()
})

test('the saved card is listed with its last digits and an add button is offered', async ({ page }) => {
  await expect(page.getByTestId(`account-payment-${SAVED_CARD.id}`)).toContainText(SAVED_CARD.last4)
  await expect(page.getByTestId('account-payment-add')).toBeVisible()
})

test('without the payment provider the save button stays off and nothing is sent', async ({ page, request }) => {
  await page.getByTestId('account-payment-add').click()
  await expect(page.getByTestId('account-payment-add-form')).toBeVisible()

  // The suite has no provider key, so the card field cannot appear: the dialog says so and cannot save.
  await expect(page.getByTestId('stripe-card-field')).toContainText('Card payments are not set up')
  await expect(page.getByTestId('account-payment-add-submit')).toBeDisabled()
  const sent = (await stubRequests(request)).find(r => r.path === '/api/me/payment-methods/sessions')
  expect(sent).toBeUndefined()
})

/** Opens the add-card dialog and picks the gateway that saves through its own page, which needs no card field. */
const openAddCardWithHostedGateway = async (page: Page): Promise<void> => {
  await page.getByTestId('account-payment-add').click()
  await expect(page.getByTestId('account-payment-add-form')).toBeVisible()
  await page.getByTestId('account-payment-module').click()
  await page.getByTestId('ui-select-option-inecobank').click()
}

test('saving without ticking the agreement is refused in the dialog and nothing reaches the backend', async ({ page, request }) => {
  await openAddCardWithHostedGateway(page)

  await page.getByTestId('account-payment-add-submit').click()

  await expect(page.getByTestId('account-payment-consent-required')).toBeVisible()
  expect((await stubRequests(request)).some(r => r.path === '/api/me/payment-methods/sessions')).toBe(false)
})

test('a refusal of the API on the save is shown as its own sentence in the dialog', async ({ page, request }) => {
  const sentence = 'E2E: you must agree to automatic charges before a card is saved.'
  await scriptStub(request, { method: HttpMethod.Post, path: '/api/me/payment-methods/sessions', status: 400, code: 'CARD_CONSENT_REQUIRED', detail: sentence })
  await openAddCardWithHostedGateway(page)
  await page.getByTestId('account-payment-consent').click()

  await page.getByTestId('account-payment-add-submit').click()

  await expect(page.getByTestId('account-payment-add-error')).toContainText(sentence)
  expect((await stubRequests(request)).some(r => r.path === '/api/me/payment-methods/sessions/complete')).toBe(false)
})

test('turning automatic charges off asks first, then reaches the backend and reads the list again', async ({ page, request }) => {
  /** Counts the payment method list requests the stub has seen so far. */
  const listReads = async (): Promise<number> =>
    (await stubRequests(request)).filter(r => r.method === 'GET' && r.path === '/api/me/payment-methods').length
  const before = await listReads()
  await expect(page.getByTestId('account-payment-auto-badge')).toBeVisible()

  await page.getByTestId('account-payment-auto-off').click()

  // Nothing is sent until the dialog is confirmed.
  await expect(page.getByTestId('ui-confirm-dialog')).toBeVisible()
  expect((await stubRequests(request)).some(r => r.method === 'DELETE' && r.path === '/api/me/payment-methods/auto-charge')).toBe(false)

  await page.getByTestId('ui-confirm-confirm').click()

  await expect(page.getByTestId('account-payment-success')).toBeVisible()
  await expect(page.getByTestId('account-payment-auto-off')).toHaveCount(0)
  await expect(page.getByTestId('account-payment-auto-badge')).toHaveCount(0)
  expect((await stubRequests(request)).some(r => r.method === 'DELETE' && r.path === '/api/me/payment-methods/auto-charge')).toBe(true)
  expect(await listReads()).toBeGreaterThan(before)
})

test('making the card the default reaches the backend and confirms', async ({ page, request }) => {
  await page.getByTestId(`account-payment-default-${SAVED_CARD.id}`).click()

  await expect(page.getByTestId('account-payment-success')).toBeVisible()
  const sent = (await stubRequests(request)).find(r => r.method === 'PUT' && r.path === `/api/me/payment-methods/${SAVED_CARD.gatewayModuleId}/${SAVED_CARD.id}`)
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
  expect((await stubRequests(request)).some(r => r.method === 'DELETE' && r.path === `/api/me/payment-methods/${SAVED_CARD.gatewayModuleId}/${SAVED_CARD.id}`)).toBe(true)
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
