/**
 * Quotes flow: the list shows each quote with its client and stage, the search box and the filters of the filter dialog
 * (client, stage, validity) are sent to the API and kept in the address, each applied filter is a chip, the tab of a client
 * offers the same search and filters, and a quote's id opens its detail page; a refused load shows the API's sentence.
 */
import { HttpMethod } from '../src/types/http'
import { expect, test } from '@playwright/test'
import type { Page } from '@playwright/test'
import { adaDetail, clientsPage } from './fixtures/clients'
import { acceptedQuote, draftQuote, quotesPage } from './fixtures/quotes'
import type { PaginatedList } from '../src/types/paginatedlist'
import type { QuoteListItem } from '../src/types/quote'
import { fieldOf } from './support/locators'
import { json, stubApi } from './support/stubApi'
import type { StubApi, StubRoute } from './support/stubApi'

/**
 * Stubs a quote list endpoint so it narrows by the same query parameters the real API takes.
 *
 * @param path - The list's exact path.
 * @returns The stubbed route.
 */
const quoteList = (path: string): StubRoute => ({
  method: HttpMethod.Get,
  path,
  respond: (call) => {
    const query = new URLSearchParams(call.search)
    const term = query.get('search')?.toLowerCase() ?? ''
    const items = quotesPage.items.filter(
      (quote) =>
        (quote.subject.toLowerCase().includes(term) || String(quote.id) === term)
        && (query.get('stage') === null || quote.stage === query.get('stage'))
        && (query.get('clientId') === null || String(quote.clientId) === query.get('clientId')),
    )
    const body: PaginatedList<QuoteListItem> = { ...quotesPage, items, totalCount: items.length }
    return { body }
  },
})

/**
 * Stubs the quote list and the client picker.
 *
 * @param page - The page under test.
 * @returns The stubbed API, to read the requests it saw.
 */
const stubList = (page: Page): Promise<StubApi> =>
  stubApi(page, {
    signedIn: true,
    routes: [quoteList('/api/billing/quotes'), json(HttpMethod.Get, '/api/clients', clientsPage)],
  })

/**
 * Chooses an option in one of the filter dialog's selects.
 *
 * @param page - The page under test.
 * @param testid - The `data-testid` of the select.
 * @param label - The exact label of the option to choose.
 * @returns Resolves once the option is clicked.
 */
const chooseOption = async (page: Page, testid: string, label: string): Promise<void> => {
  await page.getByTestId(testid).getByTestId('ui-select-trigger').click()
  await page.getByTestId('ui-select-option').filter({ hasText: new RegExp(`^${label}$`) }).click()
}

test('the search box sends the search to the API and shows only the matching quote', async ({ page }) => {
  const api = await stubList(page)

  await page.goto('/billing/quotes')
  await expect(page.getByTestId('billing-quotes-table')).toContainText(draftQuote.subject)
  await expect(page.getByTestId('billing-quotes-table')).toContainText(acceptedQuote.subject)

  // The search box writes to the address after a pause and the list reloads from it.
  await fieldOf(page, 'billing-quotes-search').fill('Migration')
  await expect(page).toHaveURL(/search=Migration/)

  await expect(page.getByTestId(`billing-quote-open-${draftQuote.id}`)).toHaveCount(0)
  await expect(page.getByTestId(`billing-quote-open-${acceptedQuote.id}`)).toBeVisible()
  expect(api.callsTo(HttpMethod.Get, '/api/billing/quotes').at(-1)?.search).toContain('search=Migration')
})

test('the stage and validity filters are sent to the API, shown as chips and removed from their chips', async ({ page }) => {
  const api = await stubList(page)

  await page.goto('/billing/quotes')
  await expect(page.getByTestId(`billing-quote-open-${draftQuote.id}`)).toBeVisible()

  await page.getByTestId('billing-quotes-filter').click()
  await chooseOption(page, 'billing-quotes-filter-stage', 'Accepted')
  await chooseOption(page, 'billing-quotes-filter-validity', 'Valid')
  await page.getByTestId('filter-apply').click()

  await expect(page).toHaveURL(/stage=Accepted/)
  await expect(page).toHaveURL(/validity=Valid/)
  await expect(page.getByTestId('filter-chip-stage')).toContainText('Accepted')
  await expect(page.getByTestId('filter-chip-validity')).toContainText('Valid')
  await expect(page.getByTestId(`billing-quote-open-${acceptedQuote.id}`)).toBeVisible()
  await expect(page.getByTestId(`billing-quote-open-${draftQuote.id}`)).toHaveCount(0)
  const sent = api.callsTo(HttpMethod.Get, '/api/billing/quotes').at(-1)?.search ?? ''
  expect(sent).toContain('stage=Accepted')
  expect(sent).toContain('validity=Valid')

  await page.getByTestId('filter-chip-remove-stage').click()

  await expect(page.getByTestId('filter-chip-stage')).toHaveCount(0)
  expect(api.callsTo(HttpMethod.Get, '/api/billing/quotes').at(-1)?.search).not.toContain('stage=')
})

test('a list link with filters opens the filtered list and names the client on its chip', async ({ page }) => {
  const api = await stubList(page)

  await page.goto(`/billing/quotes?clientId=${acceptedQuote.clientId}&stage=Accepted`)

  await expect(page.getByTestId('filter-chip-stage')).toContainText('Accepted')
  await expect(page.getByTestId('filter-chip-clientId')).toBeVisible()
  await expect(page.getByTestId(`billing-quote-open-${acceptedQuote.id}`)).toBeVisible()
  await expect(page.getByTestId(`billing-quote-open-${draftQuote.id}`)).toHaveCount(0)
  expect(api.callsTo(HttpMethod.Get, '/api/billing/quotes')[0]?.search).toContain(`clientId=${acceptedQuote.clientId}`)
})

test('the quotes tab of a client sends its search and filters to the API and shows them as chips', async ({ page }) => {
  const api = await stubApi(page, {
    signedIn: true,
    routes: [json(HttpMethod.Get, '/api/clients/7', adaDetail), quoteList('/api/billing/quotes/client/7')],
  })

  await page.goto('/clients/7/quotes')
  await expect(page.getByTestId('client-quotes-table')).toContainText(draftQuote.subject)

  await page.getByTestId('client-quotes-filter').click()
  await chooseOption(page, 'client-quotes-filter-stage', 'Accepted')
  await page.getByTestId('filter-apply').click()

  await expect(page).toHaveURL(/stage=Accepted/)
  await expect(page.getByTestId('filter-chip-stage')).toContainText('Accepted')
  await expect(page.getByTestId('client-quotes-table')).toContainText(acceptedQuote.subject)
  await expect(page.getByTestId('client-quotes-table')).not.toContainText(draftQuote.subject)
  expect(api.callsTo(HttpMethod.Get, '/api/billing/quotes/client/7').at(-1)?.search).toContain('stage=Accepted')
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
