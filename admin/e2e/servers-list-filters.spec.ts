/**
 * Servers list flow: the search, the module and state filters of the filter dialog and the page are sent to the API and
 * kept in the address, an applied filter is a chip that is removed from its own button, and a list link with a filter
 * opens the filtered list.
 */
import { HttpMethod } from '../src/types/http'
import { expect, test } from '@playwright/test'
import type { Page } from '@playwright/test'
import { hostServer, serverModules } from './fixtures/servers'
import type { PaginatedList } from '../src/types/paginatedlist'
import type { ServerDto } from '../src/types/server'
import { fieldOf } from './support/locators'
import { json, stubApi } from './support/stubApi'
import type { StubApi } from './support/stubApi'

/** The list endpoint the screen reads. */
const LIST_PATH = '/api/admin/servers'

/** A cPanel server that has never been tested. */
const cpanelServer: ServerDto = { ...hostServer, id: 1, name: 'cpanel-alpha', module: 'CPanel', isOnline: null }

/** A panel plugin server whose last test failed, the one the module and state filters must keep. */
const pluginServer: ServerDto = { ...hostServer, id: 2, name: 'hestia-beta', module: 'hestiacp', isOnline: false }

/** Every server the stubbed list holds. */
const allServers: ServerDto[] = [cpanelServer, pluginServer]

/**
 * Stubs the server list so it narrows by the same query parameters the real API takes.
 *
 * @param page - The page under test.
 * @returns The stubbed API, to read the requests it saw.
 */
const stubServerList = (page: Page): Promise<StubApi> =>
  stubApi(page, {
    signedIn: true,
    routes: [
      {
        method: HttpMethod.Get,
        path: LIST_PATH,
        respond: (call) => {
          const query = new URLSearchParams(call.search)
          const term = query.get('search')?.toLowerCase() ?? ''
          const module = query.get('module')
          const state = query.get('state')
          const items = allServers.filter(
            (server) =>
              server.name.toLowerCase().includes(term)
              && (module === null || server.module === module)
              && (state === null || (state === 'offline' && server.isOnline === false) || (state === 'untested' && server.isOnline === null)),
          )
          const body: PaginatedList<ServerDto> = { items, totalCount: items.length, pageNumber: 1, pageSize: 20, totalPages: 1 }
          return { body }
        },
      },
      json(HttpMethod.Get, '/api/admin/server-groups', []),
      json(HttpMethod.Get, '/api/admin/servers/modules', serverModules),
    ],
  })

test('searching by name sends the search and shows only the matching server', async ({ page }) => {
  const api = await stubServerList(page)

  await page.goto('/servers')
  await expect(page.getByTestId('server-row-1')).toBeVisible()
  await expect(page.getByTestId('server-row-2')).toBeVisible()

  // The search box writes to the address after a pause and the list reloads from it.
  await fieldOf(page, 'servers-search').fill('hestia')
  await expect(page).toHaveURL(/search=hestia/)

  await expect(page.getByTestId('server-row-2')).toBeVisible()
  await expect(page.getByTestId('server-row-1')).toHaveCount(0)
  expect(api.callsTo(HttpMethod.Get, LIST_PATH).at(-1)?.search).toContain('search=hestia')
})

test('the module and state filters are sent to the API, shown as chips and removed from their chips', async ({ page }) => {
  const api = await stubServerList(page)

  await page.goto('/servers')
  await expect(page.getByTestId('server-row-1')).toBeVisible()

  await page.getByTestId('filter-trigger').click()
  await page.getByTestId('servers-filter-module').getByTestId('ui-select-trigger').click()
  await page.getByTestId('ui-select-option').filter({ hasText: /^Hestia Control Panel$/ }).click()
  await page.getByTestId('servers-filter-state').getByTestId('ui-select-trigger').click()
  await page.getByTestId('ui-select-option').filter({ hasText: /^Offline$/ }).click()
  await page.getByTestId('filter-apply').click()

  await expect(page).toHaveURL(/module=hestiacp/)
  await expect(page).toHaveURL(/state=offline/)
  await expect(page.getByTestId('filter-chip-module')).toContainText('hestiacp')
  await expect(page.getByTestId('filter-chip-state')).toContainText('Offline')
  await expect(page.getByTestId('server-row-1')).toHaveCount(0)
  await expect(page.getByTestId('server-row-2')).toBeVisible()
  const sent = api.callsTo(HttpMethod.Get, LIST_PATH).at(-1)?.search
  expect(sent).toContain('module=hestiacp')
  expect(sent).toContain('state=offline')

  await page.getByTestId('filter-chip-remove-module').click()
  await page.getByTestId('filter-chip-remove-state').click()

  await expect(page.getByTestId('filter-chip-module')).toHaveCount(0)
  await expect(page.getByTestId('server-row-1')).toBeVisible()
  expect(api.callsTo(HttpMethod.Get, LIST_PATH).at(-1)?.search).not.toContain('module=')
})

test('a list link with a filter opens the filtered list', async ({ page }) => {
  const api = await stubServerList(page)

  await page.goto('/servers?state=untested')

  await expect(page.getByTestId('filter-chip-state')).toContainText('Not tested')
  await expect(page.getByTestId('server-row-1')).toBeVisible()
  await expect(page.getByTestId('server-row-2')).toHaveCount(0)
  expect(api.callsTo(HttpMethod.Get, LIST_PATH)[0]?.search).toContain('state=untested')
})

test('a filter nothing matches says so and keeps the toolbar', async ({ page }) => {
  await stubServerList(page)

  await page.goto('/servers?module=hestiacp&state=untested')

  await expect(page.getByTestId('servers-no-matches')).toBeVisible()
  await expect(page.getByTestId('servers-search')).toBeVisible()
})
