/**
 * Domains list flow: the search, the status and registrar filters of the filter dialog and the page are
 * sent to the API and kept in the address, an applied filter is a chip that is removed from its own button, and
 * the old registrations address opens the same list.
 */
import { HttpMethod } from '../src/types/http'
import { expect, test } from '@playwright/test'
import type { Page } from '@playwright/test'
import { registeredDomain } from './fixtures/orders'
import type { DomainRegistration } from '../src/types/domain'
import { DomainStatus } from '../src/types/domain'
import type { PaginatedList } from '../src/types/paginatedlist'
import { fieldOf } from './support/locators'
import { stubApi } from './support/stubApi'
import type { StubApi } from './support/stubApi'

/** An expired domain, the one the status filter must keep. */
const expiredDomain: DomainRegistration = {
  ...registeredDomain,
  id: 52,
  name: 'lapsed.example.test',
  status: DomainStatus.Expired,
}

/** Every domain the stubbed list holds. */
const allDomains: DomainRegistration[] = [registeredDomain, expiredDomain]

/**
 * Stubs the domain list so it narrows by the same query parameters the real API takes.
 *
 * @param page - The page under test.
 * @returns The stubbed API, to read the requests it saw.
 */
const stubDomainList = (page: Page): Promise<StubApi> =>
  stubApi(page, {
    signedIn: true,
    routes: [
      {
        method: HttpMethod.Get,
        path: '/api/admin/plugins',
        respond: () => ({
          body: [
            { id: 'registrar-one', name: 'Registrar One', type: 'registrar', isLoaded: true, status: 'Loaded' },
            { id: 'registrar-two', name: 'Registrar Two', type: 'registrar', isLoaded: true, status: 'Loaded' },
          ],
        }),
      },
      {
        method: HttpMethod.Get,
        path: '/api/domains',
        respond: (call) => {
          const query = new URLSearchParams(call.search)
          const term = query.get('search')?.toLowerCase() ?? ''
          const status = query.get('status')
          const items = allDomains.filter(
            (domain) => domain.name.includes(term) && (status === null || domain.status === status),
          )
          const body: PaginatedList<DomainRegistration> = {
            items,
            totalCount: items.length,
            pageNumber: 1,
            pageSize: 20,
            totalPages: 1,
          }
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

test('searching by name sends the search and shows only the matching domain', async ({ page }) => {
  const api = await stubDomainList(page)

  await page.goto('/domains')
  await expect(page.getByTestId(`domains-row-${registeredDomain.id}`)).toBeVisible()
  await expect(page.getByTestId(`domains-row-${expiredDomain.id}`)).toBeVisible()

  // The search box writes to the address after a pause and the list reloads from it.
  await fieldOf(page, 'domains-search').fill('lapsed')
  await expect(page).toHaveURL(/search=lapsed/)

  await expect(page.getByTestId(`domains-row-${expiredDomain.id}`)).toBeVisible()
  await expect(page.getByTestId(`domains-row-${registeredDomain.id}`)).toHaveCount(0)
  expect(api.callsTo(HttpMethod.Get, '/api/domains').at(-1)?.search).toContain('search=lapsed')
})

test('the status and registrar filters are sent to the API, shown as chips and removed from their chips', async ({ page }) => {
  const api = await stubDomainList(page)

  await page.goto('/domains')
  await expect(page.getByTestId(`domains-row-${registeredDomain.id}`)).toBeVisible()

  await page.getByTestId('filter-trigger').click()
  await chooseOption(page, 'domains-filter-status', 'Expired')
  await chooseOption(page, 'domains-filter-registrar', 'Registrar Two')
  await page.getByTestId('filter-apply').click()

  await expect(page).toHaveURL(/status=Expired/)
  await expect(page).toHaveURL(/registrarModule=registrar-two/)
  await expect(page.getByTestId('filter-chip-status')).toContainText('Expired')
  await expect(page.getByTestId('filter-chip-registrarModule')).toContainText('Registrar Two')
  await expect(page.getByTestId(`domains-row-${registeredDomain.id}`)).toHaveCount(0)
  await expect(page.getByTestId(`domains-row-${expiredDomain.id}`)).toBeVisible()
  const sent = api.callsTo(HttpMethod.Get, '/api/domains').at(-1)?.search ?? ''
  expect(sent).toContain('status=Expired')
  expect(sent).toContain('registrarModule=registrar-two')

  await page.getByTestId('filter-chip-remove-status').click()

  await expect(page.getByTestId('filter-chip-status')).toHaveCount(0)
  await expect(page.getByTestId(`domains-row-${registeredDomain.id}`)).toBeVisible()
  expect(api.callsTo(HttpMethod.Get, '/api/domains').at(-1)?.search).not.toContain('status=')
})

test('a list link with a filter opens the filtered list', async ({ page }) => {
  const api = await stubDomainList(page)

  await page.goto('/domains?status=Expired')

  await expect(page.getByTestId('filter-chip-status')).toContainText('Expired')
  await expect(page.getByTestId(`domains-row-${expiredDomain.id}`)).toBeVisible()
  await expect(page.getByTestId(`domains-row-${registeredDomain.id}`)).toHaveCount(0)
  expect(api.callsTo(HttpMethod.Get, '/api/domains')[0]?.search).toContain('status=Expired')
})

test('the old domain registrations address opens the domain list', async ({ page }) => {
  await stubDomainList(page)

  await page.goto('/clients/domain-registrations')

  await expect(page).toHaveURL(/\/domains$/)
  await expect(page.getByTestId('domains-table')).toBeVisible()
})
