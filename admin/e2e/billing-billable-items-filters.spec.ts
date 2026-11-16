/**
 * Billable items list flow: the search, the type and invoiced filters of the filter dialog and the page are sent to the
 * API and kept in the address on the admin list and on the billable items tab of a client, an applied filter is a chip
 * that is removed from its own button, and the admin list pages through the API, so rows past the first page are reachable.
 */
import { HttpMethod } from '../src/types/http'
import { expect, test } from '@playwright/test'
import type { Page } from '@playwright/test'
import { adaDetail, clientsPage } from './fixtures/clients'
import { emptyClientSummary } from './fixtures/clientSummary'
import { BillableItemType } from '../src/types/billableitem'
import type { BillableItem, BillableItemsResult } from '../src/types/billableitem'
import type { PaginatedList } from '../src/types/paginatedlist'
import { fieldOf } from './support/locators'
import { json, stubApi } from './support/stubApi'
import type { StubApi } from './support/stubApi'

/** A one-time charge that is still waiting to be invoiced. */
const hostingItem: BillableItem = {
  id: 11,
  clientId: 7,
  clientName: 'Ada Lovelace',
  description: 'Hosting setup',
  amount: 40,
  currency: 'USD',
  type: BillableItemType.OneTime,
  recurringPeriod: null,
  isInvoiced: false,
  invoiceId: null,
  nextDueDate: null,
  createdAt: '2026-10-01T00:00:00Z',
}

/** A recurring charge that is already invoiced: the one the type and invoiced filters must keep apart from the hosting charge. */
const supportItem: BillableItem = {
  ...hostingItem,
  id: 12,
  description: 'Support plan',
  type: BillableItemType.Recurring,
  recurringPeriod: 'monthly',
  isInvoiced: true,
  invoiceId: 90,
}

/** Every item the stubbed list holds. */
const allItems: BillableItem[] = [hostingItem, supportItem]

/**
 * Stubs the admin-wide list and the client's list so they narrow by the same query parameters the real API takes, and the
 * client lookup the client filter reads.
 *
 * @param page - The page under test.
 * @returns The stubbed API, to read the requests it saw.
 */
const stubItemLists = (page: Page): Promise<StubApi> =>
  stubApi(page, {
    signedIn: true,
    routes: [
      {
        method: HttpMethod.Get,
        path: '/api/billing/billable-items',
        respond: (call) => {
          const query = new URLSearchParams(call.search)
          const term = query.get('search')?.toLowerCase() ?? ''
          const items = allItems.filter(
            (item) =>
              `${item.id} ${item.description}`.toLowerCase().includes(term)
              && (query.get('type') === null || item.type === query.get('type'))
              && (query.get('invoiced') === null || String(item.isInvoiced) === query.get('invoiced')),
          )
          const body: PaginatedList<BillableItem> = { items, totalCount: items.length, pageNumber: 1, pageSize: 20, totalPages: 1 }
          return { body }
        },
      },
      {
        method: HttpMethod.Get,
        path: '/api/clients/7/billable-items',
        respond: (call) => {
          const query = new URLSearchParams(call.search)
          const term = query.get('search')?.toLowerCase() ?? ''
          const matching = allItems.filter(
            (item) =>
              item.description.toLowerCase().includes(term) && (query.get('type') === null || item.type === query.get('type')),
          )
          const invoiced = matching.filter((item) => item.isInvoiced)
          const body: BillableItemsResult = {
            uninvoicedItems: matching.filter((item) => !item.isInvoiced),
            uninvoicedTotal: [],
            invoicedItems: { items: invoiced, totalCount: invoiced.length, pageNumber: 1, pageSize: 20, totalPages: 1 },
          }
          return { body }
        },
      },
      json(HttpMethod.Get, '/api/clients', clientsPage),
      json(HttpMethod.Get, '/api/clients/7', adaDetail),
      json(HttpMethod.Get, '/api/clients/7/summary', emptyClientSummary),
    ],
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

test('searching by description sends the search and shows only the matching item', async ({ page }) => {
  const api = await stubItemLists(page)

  await page.goto('/billing/billable-items')
  await expect(page.getByTestId(`billing-billable-item-select-${hostingItem.id}`)).toBeVisible()
  await expect(page.getByTestId(`billing-billable-item-select-${supportItem.id}`)).toBeVisible()

  // The search box writes to the address after a pause and the list reloads from it.
  await fieldOf(page, 'billing-billable-items-search').fill('support')
  await expect(page).toHaveURL(/search=support/)

  await expect(page.getByTestId(`billing-billable-item-select-${supportItem.id}`)).toBeVisible()
  await expect(page.getByTestId(`billing-billable-item-select-${hostingItem.id}`)).toHaveCount(0)
  expect(api.callsTo(HttpMethod.Get, '/api/billing/billable-items').at(-1)?.search).toContain('search=support')
})

test('the type and invoiced filters are sent to the API, shown as chips and removed from their chips', async ({ page }) => {
  const api = await stubItemLists(page)

  await page.goto('/billing/billable-items')
  await expect(page.getByTestId(`billing-billable-item-select-${hostingItem.id}`)).toBeVisible()

  await page.getByTestId('billing-billable-items-filter-toggle').click()
  await chooseOption(page, 'billing-billable-items-filter-type', 'Recurring')
  await chooseOption(page, 'billing-billable-items-filter-invoiced', 'Yes')
  await page.getByTestId('filter-apply').click()

  await expect(page).toHaveURL(/type=Recurring/)
  await expect(page).toHaveURL(/invoiced=true/)
  await expect(page.getByTestId('filter-chip-type')).toContainText('Recurring')
  await expect(page.getByTestId('filter-chip-invoiced')).toContainText('Yes')
  await expect(page.getByTestId(`billing-billable-item-select-${hostingItem.id}`)).toHaveCount(0)
  await expect(page.getByTestId(`billing-billable-item-select-${supportItem.id}`)).toBeVisible()
  const sent = api.callsTo(HttpMethod.Get, '/api/billing/billable-items').at(-1)?.search ?? ''
  expect(sent).toContain('type=Recurring')
  expect(sent).toContain('invoiced=true')

  await page.getByTestId('filter-chip-remove-type').click()

  await expect(page.getByTestId('filter-chip-type')).toHaveCount(0)
  expect(api.callsTo(HttpMethod.Get, '/api/billing/billable-items').at(-1)?.search).not.toContain('type=')
})

test('the client filter sends the chosen client and shows its name as a chip', async ({ page }) => {
  const api = await stubItemLists(page)

  await page.goto('/billing/billable-items')
  await expect(page.getByTestId(`billing-billable-item-select-${hostingItem.id}`)).toBeVisible()

  await page.getByTestId('billing-billable-items-filter-toggle').click()
  await page.getByTestId('billing-billable-items-filter-client').getByTestId('ui-select-trigger').click()
  await page.getByTestId('ui-select-option').filter({ hasText: 'Ada' }).first().click()
  await page.getByTestId('filter-apply').click()

  await expect(page).toHaveURL(/clientId=\d+/)
  await expect(page.getByTestId('filter-chip-clientId')).toContainText('Ada')
  expect(api.callsTo(HttpMethod.Get, '/api/billing/billable-items').at(-1)?.search).toMatch(/clientId=\d+/)
})

test('a list link with a type and an invoiced state opens the filtered list', async ({ page }) => {
  const api = await stubItemLists(page)

  await page.goto('/billing/billable-items?type=Recurring&invoiced=true')

  await expect(page.getByTestId('filter-chip-type')).toContainText('Recurring')
  await expect(page.getByTestId(`billing-billable-item-select-${supportItem.id}`)).toBeVisible()
  await expect(page.getByTestId(`billing-billable-item-select-${hostingItem.id}`)).toHaveCount(0)
  const first = api.callsTo(HttpMethod.Get, '/api/billing/billable-items')[0]?.search ?? ''
  expect(first).toContain('type=Recurring')
  expect(first).toContain('invoiced=true')
})

test('the billable items tab of a client narrows both lists with the search and the type', async ({ page }) => {
  const api = await stubItemLists(page)

  await page.goto('/clients/7/billable-items')
  await expect(page.getByTestId(`client-billable-select-${hostingItem.id}`)).toBeVisible()
  await expect(page.getByTestId('client-billable-invoiced-table')).toContainText('Support plan')

  await page.getByTestId('client-billable-filter').click()
  await chooseOption(page, 'client-billable-filter-type', 'Recurring')
  await page.getByTestId('filter-apply').click()

  await expect(page).toHaveURL(/type=Recurring/)
  await expect(page.getByTestId('filter-chip-type')).toContainText('Recurring')
  await expect(page.getByTestId('client-billable-uninvoiced-empty')).toBeVisible()
  await expect(page.getByTestId('client-billable-invoiced-table')).toContainText('Support plan')
  expect(api.callsTo(HttpMethod.Get, '/api/clients/7/billable-items').at(-1)?.search).toContain('type=Recurring')

  await fieldOf(page, 'client-billable-search').fill('hosting')
  await expect(page).toHaveURL(/search=hosting/)
  expect(api.callsTo(HttpMethod.Get, '/api/clients/7/billable-items').at(-1)?.search).toContain('search=hosting')
})
