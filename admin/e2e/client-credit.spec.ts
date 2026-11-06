/**
 * Credit tab flow: the tab shows the balance and the history with the reasons, an adjustment needs an amount and a reason and posts
 * both, the balance and the new line appear after it, and a refused deduction shows the API sentence without changing the balance.
 */
import { HttpMethod } from '../src/types/http'
import { expect, test } from '@playwright/test'
import { adaDetail } from './fixtures/clients'
import { credit, creditAfter, insufficientRefusal, insufficientSentence } from './fixtures/clientCredit'
import { fieldOf } from './support/locators'
import { json, stubApi } from './support/stubApi'

test('the tab shows the balance and the history with the reasons', async ({ page }) => {
  await stubApi(page, {
    signedIn: true,
    routes: [
      json(HttpMethod.Get, '/api/clients/7', adaDetail),
      json(HttpMethod.Get, '/api/clients/7/credit', credit),
    ],
  })

  await page.goto('/clients/7/credit')

  await expect(page.getByTestId('client-credit-balance')).toContainText('25.00')
  await expect(page.getByTestId('client-credit-table')).toContainText('Goodwill after the outage')
  await expect(page.getByTestId('client-credit-table')).toContainText('Credit applied to Invoice #5')
  await expect(page.getByTestId('client-credit-change-12')).toContainText('15.00')
  await expect(page.getByTestId('client-nav-credit')).toHaveAttribute('href', '/clients/7/credit')
})

test('an adjustment needs an amount and a reason, posts both and shows the new balance and line', async ({ page }) => {
  let current = credit
  const api = await stubApi(page, {
    signedIn: true,
    routes: [
      json(HttpMethod.Get, '/api/clients/7', adaDetail),
      { method: HttpMethod.Get, path: '/api/clients/7/credit', respond: () => ({ body: current }) },
      {
        method: HttpMethod.Post,
        path: '/api/clients/7/credit/adjustments',
        respond: () => {
          current = creditAfter(10, 'Promo')
          return { status: 204 }
        },
      },
    ],
  })

  await page.goto('/clients/7/credit')
  await expect(page.getByTestId('client-credit-submit')).toBeDisabled()
  await fieldOf(page, 'client-credit-amount').fill('10')
  await expect(page.getByTestId('client-credit-submit')).toBeDisabled()
  await fieldOf(page, 'client-credit-reason').fill('Promo')
  await page.getByTestId('client-credit-submit').click()

  await expect(page.getByTestId('client-credit-feedback')).toContainText('adjusted')
  await expect(page.getByTestId('client-credit-balance')).toContainText('35.00')
  await expect(page.getByTestId('client-credit-entry-13')).toContainText('Promo')
  expect(api.callsTo(HttpMethod.Post, '/api/clients/7/credit/adjustments')[0]?.body).toEqual({ direction: 'Add', amount: 10, reason: 'Promo' })
})

test('a deduction larger than the balance shows the API sentence and leaves the balance', async ({ page }) => {
  await stubApi(page, {
    signedIn: true,
    routes: [
      json(HttpMethod.Get, '/api/clients/7', adaDetail),
      json(HttpMethod.Get, '/api/clients/7/credit', credit),
      { method: HttpMethod.Post, path: '/api/clients/7/credit/adjustments', respond: () => ({ status: 400, body: insufficientRefusal }) },
    ],
  })

  await page.goto('/clients/7/credit')
  await page.getByTestId('client-credit-direction').getByTestId('ui-select-trigger').click()
  await page.getByTestId('ui-select-option').filter({ hasText: 'Deduct credit' }).click()
  await fieldOf(page, 'client-credit-amount').fill('500')
  await fieldOf(page, 'client-credit-reason').fill('Too much')
  await page.getByTestId('client-credit-submit').click()

  await expect(page.getByTestId('client-credit-feedback')).toHaveText(insufficientSentence)
  await expect(page.getByTestId('client-credit-balance')).toContainText('25.00')
})
