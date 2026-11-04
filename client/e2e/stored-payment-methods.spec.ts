/**
 * Flow: saved payment methods on the account's payment tab.
 *
 * The payer picks a gateway and must tick the consent before a save can start; the save hands the
 * browser to the gateway's page, and on return the pending session is completed and the new method
 * listed. A saved method can be made the default and its permission withdrawn after a confirmation.
 */
import type { Page } from '@playwright/test'
import { expect, test } from './support/test'
import {
  GATEWAY_PAGE_URL,
  HOSTED_GATEWAY,
  STORED_CARD_DEFAULT,
  STORED_CARD_NEW,
  STORED_CARD_OTHER,
  STORED_SESSION_ID,
} from './support/fixtures.mjs'
import { enableHostedGateway, resetStub, signIn, stubRequests, waitForHydration } from './support/helpers'

test.beforeEach(async ({ context, request, baseURL }) => {
  await resetStub(request)
  await enableHostedGateway(request)
  await signIn(context, baseURL ?? 'http://127.0.0.1:3100')
})

/**
 * Opens the account's payment tab, optionally through the address the gateway returns the payer to.
 *
 * @param page The page to drive.
 * @param returned Whether to arrive with the return marker, as the gateway sends the payer back.
 * @returns Resolves once the saved-methods card is on screen.
 */
const openPaymentTab = async (page: Page, returned = false): Promise<void> => {
  await page.goto(returned ? '/client/account?tab=payment&storedReturn=1' : '/client/account?tab=payment')
  await waitForHydration(page)
  await expect(page.getByTestId('stored-methods-card')).toBeVisible()
}

test('the saved methods are listed and the default one is marked', async ({ page }) => {
  await openPaymentTab(page)

  await expect(page.getByTestId(`stored-method-${STORED_CARD_DEFAULT.consentId}`)).toContainText(STORED_CARD_DEFAULT.last4)
  await expect(page.getByTestId(`stored-method-default-badge-${STORED_CARD_DEFAULT.consentId}`)).toBeVisible()
  await expect(page.getByTestId(`stored-method-default-${STORED_CARD_DEFAULT.consentId}`)).toHaveCount(0)
  await expect(page.getByTestId(`stored-method-default-badge-${STORED_CARD_OTHER.consentId}`)).toHaveCount(0)
})

test('a save cannot start without the consent, and with it the browser is sent to the gateway page', async ({ page, request }) => {
  await openPaymentTab(page)

  await page.getByTestId('stored-methods-gateway').click()
  await page.getByTestId(`ui-select-option-${HOSTED_GATEWAY.module}`).click()
  await expect(page.getByTestId('stored-methods-start')).toBeDisabled()

  await page.getByTestId('stored-methods-consent').click()
  await expect(page.getByTestId('stored-methods-start')).toBeEnabled()

  const leaving = page.waitForRequest(r => r.url().startsWith(GATEWAY_PAGE_URL))
  await page.getByTestId('stored-methods-start').click()
  await leaving

  const sent = (await stubRequests(request)).find(r => r.method === 'POST' && r.path === '/api/me/stored-payment-methods/sessions')
  expect(sent?.body).toMatchObject({ gatewayModuleId: HOSTED_GATEWAY.module, consentAccepted: true })
})

test('on return the pending session is completed, the new method is listed and the marker is dropped', async ({ page, request }) => {
  await page.addInitScript(([key, value]) => window.sessionStorage.setItem(key, value), [
    'pendingStoredSession',
    JSON.stringify({ gatewayModuleId: HOSTED_GATEWAY.module, sessionId: STORED_SESSION_ID }),
  ] as [string, string])

  await openPaymentTab(page, true)

  await expect(page.getByTestId('stored-methods-success')).toBeVisible()
  await expect(page.getByTestId(`stored-method-${STORED_CARD_NEW.consentId}`)).toContainText(STORED_CARD_NEW.last4)
  await expect(page).not.toHaveURL(/storedReturn/)

  const sent = (await stubRequests(request)).find(r => r.method === 'POST' && r.path === `/api/me/stored-payment-methods/sessions/${STORED_SESSION_ID}/complete`)
  expect(sent?.body).toMatchObject({ gatewayModuleId: HOSTED_GATEWAY.module })
})

test('a return without a pending session just shows the list', async ({ page }) => {
  await openPaymentTab(page, true)

  await expect(page.getByTestId(`stored-method-${STORED_CARD_DEFAULT.consentId}`)).toBeVisible()
  await expect(page.getByTestId('stored-methods-error')).toHaveCount(0)
})

test('making another method the default moves the badge', async ({ page, request }) => {
  await openPaymentTab(page)

  await page.getByTestId(`stored-method-default-${STORED_CARD_OTHER.consentId}`).click()

  await expect(page.getByTestId(`stored-method-default-badge-${STORED_CARD_OTHER.consentId}`)).toBeVisible()
  await expect(page.getByTestId(`stored-method-default-badge-${STORED_CARD_DEFAULT.consentId}`)).toHaveCount(0)
  await expect(page.getByTestId('stored-methods-success')).toBeVisible()
  const sent = (await stubRequests(request)).some(r => r.method === 'POST' && r.path === `/api/me/stored-payment-methods/consents/${STORED_CARD_OTHER.consentId}/default`)
  expect(sent).toBe(true)
})

test('withdrawing a permission asks first, then drops the method', async ({ page, request }) => {
  await openPaymentTab(page)

  await page.getByTestId(`stored-method-revoke-${STORED_CARD_OTHER.consentId}`).click()
  await expect(page.getByTestId('ui-confirm-dialog')).toBeVisible()
  await page.getByTestId('ui-confirm-confirm').click()

  await expect(page.getByTestId(`stored-method-${STORED_CARD_OTHER.consentId}`)).toHaveCount(0)
  await expect(page.getByTestId('stored-methods-success')).toBeVisible()
  const sent = (await stubRequests(request)).some(r => r.method === 'DELETE' && r.path === `/api/me/stored-payment-methods/consents/${STORED_CARD_OTHER.consentId}`)
  expect(sent).toBe(true)
})
