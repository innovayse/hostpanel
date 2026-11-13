/**
 * Quotes flow: the list shows each quote with its client and stage, the subject filter narrows it,
 * and a quote's id opens its detail page; a refused load shows the API's sentence.
 */
import { HttpMethod } from '../src/types/http'
import { expect, test } from '@playwright/test'
import { acceptedQuote, draftQuote, quotesPage } from './fixtures/quotes'
import { fieldOf } from './support/locators'
import { json, stubApi } from './support/stubApi'

test('the search box narrows the quotes list', async ({ page }) => {
  await stubApi(page, {
    signedIn: true,
    routes: [json(HttpMethod.Get, '/api/billing/quotes', quotesPage), json(HttpMethod.Get, '/api/clients', { items: [], pageNumber: 1, pageSize: 100, totalCount: 0, totalPages: 0 })],
  })

  await page.goto('/billing/quotes')
  await expect(page.getByTestId('billing-quotes-table')).toContainText(draftQuote.subject)
  await expect(page.getByTestId('billing-quotes-table')).toContainText(acceptedQuote.subject)

  await fieldOf(page, 'billing-quotes-search').fill('Migration')

  await expect(page.getByTestId(`billing-quote-open-${draftQuote.id}`)).toHaveCount(0)
  await expect(page.getByTestId(`billing-quote-open-${acceptedQuote.id}`)).toBeVisible()
})

test('a quote id opens the quote detail', async ({ page }) => {
  await stubApi(page, {
    signedIn: true,
    routes: [
      json(HttpMethod.Get, '/api/billing/quotes', quotesPage),
      json(HttpMethod.Get, '/api/clients', { items: [], pageNumber: 1, pageSize: 100, totalCount: 0, totalPages: 0 }),
      json(HttpMethod.Get, `/api/billing/quotes/${draftQuote.id}`, { ...draftQuote, items: [], notes: '' }),
    ],
  })

  await page.goto('/billing/quotes')
  await page.getByTestId(`billing-quote-open-${draftQuote.id}`).click()

  await page.waitForURL(`**/billing/quotes/${draftQuote.id}`)
})

test('a refused load shows the API sentence', async ({ page }) => {
  const sentence = 'You are not allowed to list quotes.'
  await stubApi(page, {
    signedIn: true,
    routes: [
      json(HttpMethod.Get, '/api/billing/quotes', { type: 'FORBIDDEN', title: 'Forbidden', status: 403, detail: sentence }, 403),
      json(HttpMethod.Get, '/api/clients', { items: [], pageNumber: 1, pageSize: 100, totalCount: 0, totalPages: 0 }),
    ],
  })

  await page.goto('/billing/quotes')

  await expect(page.getByTestId('billing-quotes-error')).toContainText(sentence)
})
