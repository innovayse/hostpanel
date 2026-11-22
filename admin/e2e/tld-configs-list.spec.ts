/**
 * TLD pricing list flow: the search, the registrar and enabled filters of the filter dialog and the page are sent to
 * the API and kept in the address, and an applied filter is a chip that is removed from its own button.
 */
import { HttpMethod } from '../src/types/http'
import { expect, test } from '@playwright/test'
import type { Page } from '@playwright/test'
import type { PaginatedList } from '../src/types/paginatedlist'
import type { TldConfigListItem } from '../src/types/tldconfig'
import { fieldOf } from './support/locators'
import { stubApi } from './support/stubApi'
import type { StubApi } from './support/stubApi'

/** The list endpoint the screen reads. */
const LIST_PATH = '/api/admin/tld-configs'

/**
 * Builds a list row.
 *
 * @param id - The row id.
 * @param tld - The extension.
 * @param registrarModule - The registrar module the TLD is sourced from.
 * @param isEnabled - Whether the TLD is on sale.
 * @returns The row.
 */
const tldRow = (id: number, tld: string, registrarModule: string, isEnabled: boolean): TldConfigListItem => ({
  id,
  tld,
  registrarModule,
  isEnabled,
  costRegister1yr: 10,
  sellRegister1yr: 12,
  marginPercent: 20,
  currency: 'USD',
  sellCurrency: 'USD',
  categories: [],
  lastSyncedAt: null,
  sortOrder: id,
})

/** An enabled TLD at the first registrar. */
const comRow = tldRow(1, 'com', 'registrar-one', true)

/** A switched-off TLD at the second registrar, the one the filters must keep. */
const netRow = tldRow(2, 'net', 'registrar-two', false)

/** Every TLD the stubbed list holds. */
const allRows: TldConfigListItem[] = [comRow, netRow]

/**
 * Stubs the TLD list so it narrows by the same query parameters the real API takes.
 *
 * @param page - The page under test.
 * @returns The stubbed API, to read the requests it saw.
 */
const stubTldList = (page: Page): Promise<StubApi> =>
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
        path: LIST_PATH,
        respond: (call) => {
          const query = new URLSearchParams(call.search)
          const term = query.get('search')?.toLowerCase() ?? ''
          const registrar = query.get('registrarModule')
          const enabled = query.get('enabled')
          const items = allRows.filter(
            (row) =>
              row.tld.includes(term)
              && (registrar === null || row.registrarModule === registrar)
              && (enabled === null || String(row.isEnabled) === enabled),
          )
          const body: PaginatedList<TldConfigListItem> = {
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

test('searching by extension sends the search and shows only the matching TLD', async ({ page }) => {
  const api = await stubTldList(page)

  await page.goto('/settings/tld-pricing')
  await expect(page.getByTestId('tld-row-com')).toBeVisible()
  await expect(page.getByTestId('tld-row-net')).toBeVisible()

  // The search box writes to the address after a pause and the list reloads from it.
  await fieldOf(page, 'tld-configs-search').fill('net')
  await expect(page).toHaveURL(/search=net/)

  await expect(page.getByTestId('tld-row-net')).toBeVisible()
  await expect(page.getByTestId('tld-row-com')).toHaveCount(0)
  expect(api.callsTo(HttpMethod.Get, LIST_PATH).at(-1)?.search).toContain('search=net')
})

test('the registrar and enabled filters are sent to the API, shown as chips and removed from their chips', async ({ page }) => {
  const api = await stubTldList(page)

  await page.goto('/settings/tld-pricing')
  await expect(page.getByTestId('tld-row-com')).toBeVisible()

  await page.getByTestId('filter-trigger').click()
  await chooseOption(page, 'tld-configs-filter-registrar', 'Registrar Two')
  await chooseOption(page, 'tld-configs-filter-enabled', 'Disabled')
  await page.getByTestId('filter-apply').click()

  await expect(page).toHaveURL(/registrarModule=registrar-two/)
  await expect(page).toHaveURL(/enabled=disabled/)
  await expect(page.getByTestId('filter-chip-registrarModule')).toContainText('Registrar Two')
  await expect(page.getByTestId('filter-chip-enabled')).toContainText('Disabled')
  await expect(page.getByTestId('tld-row-com')).toHaveCount(0)
  await expect(page.getByTestId('tld-row-net')).toBeVisible()
  const sent = api.callsTo(HttpMethod.Get, LIST_PATH).at(-1)?.search ?? ''
  expect(sent).toContain('registrarModule=registrar-two')
  expect(sent).toContain('enabled=false')

  await page.getByTestId('filter-chip-remove-registrarModule').click()

  await expect(page.getByTestId('filter-chip-registrarModule')).toHaveCount(0)
  expect(api.callsTo(HttpMethod.Get, LIST_PATH).at(-1)?.search).not.toContain('registrarModule=')
})

test('a list link with a filter opens the filtered list', async ({ page }) => {
  const api = await stubTldList(page)

  await page.goto('/settings/tld-pricing?enabled=enabled')

  await expect(page.getByTestId('filter-chip-enabled')).toContainText('Enabled')
  await expect(page.getByTestId('tld-row-com')).toBeVisible()
  await expect(page.getByTestId('tld-row-net')).toHaveCount(0)
  expect(api.callsTo(HttpMethod.Get, LIST_PATH)[0]?.search).toContain('enabled=true')
})
