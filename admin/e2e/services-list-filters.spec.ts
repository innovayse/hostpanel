/**
 * Services list flow: the search, the status, product and billing cycle filters of the filter dialog and the page
 * are sent to the API and kept in the address on the admin list and on the services tab of a client, and an
 * applied filter is a chip that is removed from its own button.
 */
import { HttpMethod } from '../src/types/http'
import { expect, test } from '@playwright/test'
import type { Page } from '@playwright/test'
import { adaDetail } from './fixtures/clients'
import { emptyClientSummary } from './fixtures/clientSummary'
import { hostingProductName, hostingService } from './fixtures/orders'
import type { PaginatedList } from '../src/types/paginatedlist'
import { BillingCycle, ServiceStatus } from '../src/types/service'
import type { ServiceListItem } from '../src/types/service'
import { fieldOf } from './support/locators'
import { json, stubApi } from './support/stubApi'
import type { StubApi } from './support/stubApi'

/** Name of the product of the suspended, annually billed service. */
const vpsProductName = 'VPS Small'

/** A suspended service on an annual cycle, the one the filters must keep apart from the hosting service. */
const suspendedService: ServiceListItem = {
  ...hostingService,
  id: 42,
  productName: vpsProductName,
  domain: 'lathe.example.test',
  billingCycle: BillingCycle.Annual,
  status: ServiceStatus.Suspended,
}

/** Every service the stubbed list holds. */
const allServices: ServiceListItem[] = [hostingService, suspendedService]

/**
 * Stubs the service list so it narrows by the same query parameters the real API takes, and the product lookup the
 * filter dialog reads.
 *
 * @param page - The page under test.
 * @returns The stubbed API, to read the requests it saw.
 */
const stubServiceList = (page: Page): Promise<StubApi> =>
  stubApi(page, {
    signedIn: true,
    routes: [
      {
        method: HttpMethod.Get,
        path: '/api/services',
        respond: (call) => {
          const query = new URLSearchParams(call.search)
          const term = query.get('search')?.toLowerCase() ?? ''
          const status = query.get('status')
          const billingCycle = query.get('billingCycle')
          const items = allServices.filter(
            (service) =>
              `${service.id} ${service.domain ?? ''} ${service.productName}`.toLowerCase().includes(term)
              && (status === null || service.status === status)
              && (billingCycle === null || service.billingCycle === billingCycle),
          )
          const body: PaginatedList<ServiceListItem> = {
            items,
            totalCount: items.length,
            pageNumber: 1,
            pageSize: 20,
            totalPages: 1,
          }
          return { body }
        },
      },
      json(HttpMethod.Get, '/api/products', [
        { id: 1, name: hostingProductName },
        { id: 2, name: vpsProductName },
      ]),
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

test('searching by domain sends the search and shows only the matching service', async ({ page }) => {
  const api = await stubServiceList(page)

  await page.goto('/services')
  await expect(page.getByTestId(`services-row-${hostingService.id}`)).toBeVisible()
  await expect(page.getByTestId(`services-row-${suspendedService.id}`)).toBeVisible()

  // The search box writes to the address after a pause and the list reloads from it.
  await fieldOf(page, 'services-search').fill('lathe')
  await expect(page).toHaveURL(/search=lathe/)

  await expect(page.getByTestId(`services-row-${suspendedService.id}`)).toBeVisible()
  await expect(page.getByTestId(`services-row-${hostingService.id}`)).toHaveCount(0)
  expect(api.callsTo(HttpMethod.Get, '/api/services').at(-1)?.search).toContain('search=lathe')
})

test('the status, product and billing cycle filters are sent to the API, shown as chips and removed from their chips', async ({ page }) => {
  const api = await stubServiceList(page)

  await page.goto('/services')
  await expect(page.getByTestId(`services-row-${hostingService.id}`)).toBeVisible()

  await page.getByTestId('services-filter').click()
  await chooseOption(page, 'services-filter-status', 'Suspended')
  await chooseOption(page, 'services-filter-product', vpsProductName)
  await chooseOption(page, 'services-filter-billing-cycle', 'Annual')
  await page.getByTestId('filter-apply').click()

  await expect(page).toHaveURL(/status=Suspended/)
  await expect(page).toHaveURL(/productId=2/)
  await expect(page).toHaveURL(/billingCycle=Annual/)
  await expect(page.getByTestId('filter-chip-status')).toContainText('Suspended')
  await expect(page.getByTestId('filter-chip-productId')).toContainText(vpsProductName)
  await expect(page.getByTestId('filter-chip-billingCycle')).toContainText('Annual')
  await expect(page.getByTestId(`services-row-${hostingService.id}`)).toHaveCount(0)
  await expect(page.getByTestId(`services-row-${suspendedService.id}`)).toBeVisible()
  const sent = api.callsTo(HttpMethod.Get, '/api/services').at(-1)?.search ?? ''
  expect(sent).toContain('status=Suspended')
  expect(sent).toContain('productId=2')
  expect(sent).toContain('billingCycle=Annual')

  await page.getByTestId('filter-chip-remove-status').click()

  await expect(page.getByTestId('filter-chip-status')).toHaveCount(0)
  expect(api.callsTo(HttpMethod.Get, '/api/services').at(-1)?.search).not.toContain('status=')
})

test('a list link with a filter opens the filtered list', async ({ page }) => {
  const api = await stubServiceList(page)

  await page.goto('/services?status=Suspended')

  await expect(page.getByTestId('filter-chip-status')).toContainText('Suspended')
  await expect(page.getByTestId(`services-row-${suspendedService.id}`)).toBeVisible()
  await expect(page.getByTestId(`services-row-${hostingService.id}`)).toHaveCount(0)
  expect(api.callsTo(HttpMethod.Get, '/api/services')[0]?.search).toContain('status=Suspended')
})

test('the services tab of a client searches and filters with the client kept in the request', async ({ page }) => {
  const api = await stubServiceList(page)

  await page.goto('/clients/7/services')
  await expect(page.getByTestId(`client-services-row-${hostingService.id}`)).toBeVisible()

  await page.getByTestId('client-services-filter').click()
  await chooseOption(page, 'client-services-filter-billing-cycle', 'Annual')
  await page.getByTestId('filter-apply').click()

  await expect(page).toHaveURL(/billingCycle=Annual/)
  await expect(page.getByTestId(`client-services-row-${suspendedService.id}`)).toBeVisible()
  await expect(page.getByTestId(`client-services-row-${hostingService.id}`)).toHaveCount(0)
  const sent = api.callsTo(HttpMethod.Get, '/api/services').at(-1)?.search ?? ''
  expect(sent).toContain('clientId=7')
  expect(sent).toContain('billingCycle=Annual')

  await fieldOf(page, 'client-services-search').fill('lathe')
  await expect(page).toHaveURL(/search=lathe/)
  expect(api.callsTo(HttpMethod.Get, '/api/services').at(-1)?.search).toContain('search=lathe')
})
