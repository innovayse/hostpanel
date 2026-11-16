/**
 * Fraud block lists flow: the tab and the search live in the address and are sent to the API together, a tab starts
 * from an empty search, and a link with a tab and a search opens that list narrowed.
 */
import { HttpMethod } from '../src/types/http'
import { expect, test } from '@playwright/test'
import type { Page } from '@playwright/test'
import { BlockListType } from '../src/types/fraud'
import type { BlockListEntry } from '../src/types/fraud'
import type { PaginatedList } from '../src/types/paginatedlist'
import { fieldOf } from './support/locators'
import { stubApi } from './support/stubApi'
import type { StubApi } from './support/stubApi'

/**
 * Builds a block list entry.
 *
 * @param id - Entry id.
 * @param type - What the value names.
 * @param value - The blocked value.
 * @param note - Why it is blocked, null for none.
 * @returns The entry.
 */
const entryOf = (id: number, type: BlockListType, value: string, note: string | null): BlockListEntry => ({
  id,
  type,
  value,
  note,
  createdAt: '2026-11-01T10:00:00Z',
})

/** Every entry the stubbed block lists hold. */
const allEntries: BlockListEntry[] = [
  entryOf(1, BlockListType.Ip, '203.0.113.7', 'spam run'),
  entryOf(2, BlockListType.Ip, '198.51.100.9', 'chargebacks'),
  entryOf(3, BlockListType.EmailDomain, 'junk.example', 'spam run'),
]

/**
 * Stubs the block lists so they narrow by the same query parameters the real API takes.
 *
 * @param page - The page under test.
 * @returns The stubbed API, to read the requests it saw.
 */
const stubBlockLists = (page: Page): Promise<StubApi> =>
  stubApi(page, {
    signedIn: true,
    routes: [
      {
        method: HttpMethod.Get,
        path: '/api/admin/fraud/blocklist',
        respond: (call) => {
          const query = new URLSearchParams(call.search)
          const term = query.get('search')?.toLowerCase() ?? ''
          const items = allEntries.filter(
            (entry) => entry.type === query.get('type') && `${entry.value} ${entry.note ?? ''}`.toLowerCase().includes(term),
          )
          const body: PaginatedList<BlockListEntry> = { items, totalCount: items.length, pageNumber: 1, pageSize: 20, totalPages: 1 }
          return { body }
        },
      },
    ],
  })

test('searching sends the search with the list on show and shows only the matching entry', async ({ page }) => {
  const api = await stubBlockLists(page)

  await page.goto('/fraud/block-lists')
  await expect(page.getByTestId('fraud-block-list-row-1')).toBeVisible()
  await expect(page.getByTestId('fraud-block-list-row-2')).toBeVisible()

  // The search box writes to the address after a pause and the list reloads from it.
  await fieldOf(page, 'fraud-block-list-search').fill('chargebacks')
  await expect(page).toHaveURL(/search=chargebacks/)

  await expect(page.getByTestId('fraud-block-list-row-2')).toBeVisible()
  await expect(page.getByTestId('fraud-block-list-row-1')).toHaveCount(0)
  const last = api.callsTo(HttpMethod.Get, '/api/admin/fraud/blocklist').at(-1)?.search ?? ''
  expect(last).toContain('type=Ip')
  expect(last).toContain('search=chargebacks')
})

test('another tab keeps the tab in the address and starts from an empty search', async ({ page }) => {
  const api = await stubBlockLists(page)

  await page.goto('/fraud/block-lists?search=spam')
  await expect(page.getByTestId('fraud-block-list-row-1')).toBeVisible()
  await expect(fieldOf(page, 'fraud-block-list-search')).toHaveValue('spam')

  await page.getByTestId('fraud-block-list-tab-EmailDomain').click()

  await expect(page).toHaveURL(/type=EmailDomain/)
  await expect(page).not.toHaveURL(/search=/)
  await expect(fieldOf(page, 'fraud-block-list-search')).toHaveValue('')
  await expect(page.getByTestId('fraud-block-list-row-3')).toBeVisible()
  const last = api.callsTo(HttpMethod.Get, '/api/admin/fraud/blocklist').at(-1)?.search ?? ''
  expect(last).toContain('type=EmailDomain')
  expect(last).not.toContain('search=')
})

test('a list link with a tab and a search opens that list narrowed', async ({ page }) => {
  const api = await stubBlockLists(page)

  await page.goto('/fraud/block-lists?type=EmailDomain&search=junk')

  await expect(page.getByTestId('fraud-block-list-tab-EmailDomain')).toHaveAttribute('aria-selected', 'true')
  await expect(page.getByTestId('fraud-block-list-row-3')).toBeVisible()
  const first = api.callsTo(HttpMethod.Get, '/api/admin/fraud/blocklist')[0]?.search ?? ''
  expect(first).toContain('type=EmailDomain')
  expect(first).toContain('search=junk')
})
