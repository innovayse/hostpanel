/**
 * Cancellation requests list flow: the search on the reason, the type and status filters of the filter dialog and the
 * page are sent to the API and kept in the address, an applied filter is a chip that is removed from its own button, and
 * a link with filters opens the narrowed list.
 */
import { HttpMethod } from '../src/types/http'
import { expect, test } from '@playwright/test'
import type { Page } from '@playwright/test'
import type { PaginatedList } from '../src/types/paginatedlist'
import { CancellationStatus, CancellationType } from '../src/types/service'
import type { CancellationRequestItem } from '../src/types/service'
import { fieldOf } from './support/locators'
import { stubApi } from './support/stubApi'
import type { StubApi } from './support/stubApi'

/** An open request to stop the service at once: the one the filters must keep apart from the closed one. */
const openRequest: CancellationRequestItem = {
  id: 21,
  serviceId: 3,
  serviceName: 'Shared Hosting',
  clientId: 7,
  clientName: 'Ada Lovelace',
  type: CancellationType.Immediate,
  reason: 'Moving abroad',
  status: CancellationStatus.Open,
  createdAt: '2026-10-01T09:00:00Z',
}

/** A handled request to stop at the end of the period. */
const closedRequest: CancellationRequestItem = {
  ...openRequest,
  id: 22,
  serviceName: 'VPS Small',
  type: CancellationType.EndOfBillingPeriod,
  reason: 'Too expensive',
  status: CancellationStatus.Closed,
}

/** Every request the stubbed list holds. */
const allRequests: CancellationRequestItem[] = [openRequest, closedRequest]

/**
 * Stubs the cancellation request list so it narrows by the same query parameters the real API takes.
 *
 * @param page - The page under test.
 * @returns The stubbed API, to read the requests it saw.
 */
const stubRequestList = (page: Page): Promise<StubApi> =>
  stubApi(page, {
    signedIn: true,
    routes: [
      {
        method: HttpMethod.Get,
        path: '/api/admin/cancellation-requests',
        respond: (call) => {
          const query = new URLSearchParams(call.search)
          const term = query.get('search')?.toLowerCase() ?? ''
          const items = allRequests.filter(
            (request) =>
              `${request.id} ${request.reason ?? ''}`.toLowerCase().includes(term)
              && (query.get('type') === null || request.type === query.get('type'))
              && (query.get('status') === null || request.status === query.get('status')),
          )
          const body: PaginatedList<CancellationRequestItem> = { items, totalCount: items.length, pageNumber: 1, pageSize: 20, totalPages: 1 }
          return { body }
        },
      },
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

test('searching by reason sends the search and shows only the matching request', async ({ page }) => {
  const api = await stubRequestList(page)

  await page.goto('/clients/cancellations')
  await expect(page.getByTestId(`cancellations-row-${openRequest.id}`)).toBeVisible()
  await expect(page.getByTestId(`cancellations-row-${closedRequest.id}`)).toBeVisible()

  // The search box writes to the address after a pause and the list reloads from it.
  await fieldOf(page, 'cancellations-search').fill('expensive')
  await expect(page).toHaveURL(/search=expensive/)

  await expect(page.getByTestId(`cancellations-row-${closedRequest.id}`)).toBeVisible()
  await expect(page.getByTestId(`cancellations-row-${openRequest.id}`)).toHaveCount(0)
  expect(api.callsTo(HttpMethod.Get, '/api/admin/cancellation-requests').at(-1)?.search).toContain('search=expensive')
})

test('the type and status filters are sent to the API, shown as chips and removed from their chips', async ({ page }) => {
  const api = await stubRequestList(page)

  await page.goto('/clients/cancellations')
  await expect(page.getByTestId(`cancellations-row-${openRequest.id}`)).toBeVisible()

  await page.getByTestId('cancellations-filter').click()
  await chooseOption(page, 'cancellations-filter-type', 'End of Billing Period')
  await chooseOption(page, 'cancellations-filter-status', 'Closed')
  await page.getByTestId('filter-apply').click()

  await expect(page).toHaveURL(/type=EndOfBillingPeriod/)
  await expect(page).toHaveURL(/status=Closed/)
  await expect(page.getByTestId('filter-chip-type')).toContainText('End of Billing Period')
  await expect(page.getByTestId('filter-chip-status')).toContainText('Closed')
  await expect(page.getByTestId(`cancellations-row-${openRequest.id}`)).toHaveCount(0)
  await expect(page.getByTestId(`cancellations-row-${closedRequest.id}`)).toBeVisible()
  const sent = api.callsTo(HttpMethod.Get, '/api/admin/cancellation-requests').at(-1)?.search ?? ''
  expect(sent).toContain('type=EndOfBillingPeriod')
  expect(sent).toContain('status=Closed')

  await page.getByTestId('filter-chip-remove-status').click()

  await expect(page.getByTestId('filter-chip-status')).toHaveCount(0)
  expect(api.callsTo(HttpMethod.Get, '/api/admin/cancellation-requests').at(-1)?.search).not.toContain('status=')
})

test('a list link with a status opens the filtered list', async ({ page }) => {
  const api = await stubRequestList(page)

  await page.goto('/clients/cancellations?status=Open')

  await expect(page.getByTestId('filter-chip-status')).toContainText('Open')
  await expect(page.getByTestId(`cancellations-row-${openRequest.id}`)).toBeVisible()
  await expect(page.getByTestId(`cancellations-row-${closedRequest.id}`)).toHaveCount(0)
  expect(api.callsTo(HttpMethod.Get, '/api/admin/cancellation-requests')[0]?.search).toContain('status=Open')
})
