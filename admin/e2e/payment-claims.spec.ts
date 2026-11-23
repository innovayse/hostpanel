/**
 * Held payment claims flow: the screen lists the held claims the API returns, shows the empty state
 * when there are none, and on a refused load shows the API sentence without the empty state.
 */
import { HttpMethod } from '../src/types/http'
import { expect, test } from '@playwright/test'
import { claimsRefusal, claimsRefusalSentence, emptyClaimsPage, heldClaimsPage } from './fixtures/paymentClaims'
import { json, stubApi } from './support/stubApi'

test('the payment claims screen lists the held claims', async ({ page }) => {
  const api = await stubApi(page, {
    signedIn: true,
    routes: [json(HttpMethod.Get, '/api/billing/payment-claims', heldClaimsPage)],
  })

  await page.goto('/billing/payment-claims')

  await expect(page.getByTestId('billing-payment-claims-table')).toContainText('Ada Lovelace')
  await expect(page.getByTestId('billing-payment-claims-table')).toContainText('Grace Hopper')
  await expect(page.getByTestId('billing-payment-claims-table')).toContainText('$12.50')
  await expect(page.getByTestId('billing-payment-claim-invoice-3')).toHaveAttribute('href', '/billing/5')
  expect(api.callsTo(HttpMethod.Get, '/api/billing/payment-claims')[0]?.search).toContain('status=Held')
})

test('the payment claims screen shows the empty state when nothing is held', async ({ page }) => {
  await stubApi(page, {
    signedIn: true,
    routes: [json(HttpMethod.Get, '/api/billing/payment-claims', emptyClaimsPage)],
  })

  await page.goto('/billing/payment-claims')

  await expect(page.getByTestId('billing-payment-claims-table')).toContainText('No held payment claims.')
})

test('a refused load shows the API sentence and no empty state', async ({ page }) => {
  await stubApi(page, {
    signedIn: true,
    routes: [json(HttpMethod.Get, '/api/billing/payment-claims', claimsRefusal, 403)],
  })

  await page.goto('/billing/payment-claims')

  await expect(page.getByTestId('billing-payment-claims-error')).toContainText(claimsRefusalSentence)
  await expect(page.getByTestId('billing-payment-claims-table')).not.toContainText('No held payment claims.')
})
