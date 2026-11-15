/**
 * Support list flows: the announcements, the network issues and the knowledgebase articles each send their search and
 * filters to the API and keep them in the address, show each applied filter as a chip that is removed from its own
 * button, and a link with filters opens the filtered list. The network issues have no status tabs any more: the status
 * is one of the filters.
 */
import { HttpMethod } from '../src/types/http'
import { expect, test } from '@playwright/test'
import type { Page } from '@playwright/test'
import type { Announcement } from '../src/types/announcement'
import type { KbArticle, KbCategory } from '../src/types/kbarticle'
import { NetworkIssuePriority, NetworkIssueStatus, NetworkIssueType } from '../src/types/networkissue'
import type { NetworkIssue } from '../src/types/networkissue'
import type { PaginatedList } from '../src/types/paginatedlist'
import { fieldOf } from './support/locators'
import { json, stubApi } from './support/stubApi'
import type { StubRoute } from './support/stubApi'

/** Every announcement the stub holds. */
const announcements: Announcement[] = [
  { id: 1, title: 'Scheduled maintenance', content: 'The panel is read-only on Sunday night.', isPublished: true, createdAt: '2026-10-14T09:00:00Z' },
  { id: 2, title: 'New data center', content: 'Draft text.', isPublished: false, createdAt: '2026-10-13T09:00:00Z' },
]

/** Every network issue the stub holds. */
const issues: NetworkIssue[] = [
  {
    id: 1,
    title: 'Packet loss',
    type: NetworkIssueType.Server,
    server: 'srv-1',
    priority: NetworkIssuePriority.Critical,
    status: NetworkIssueStatus.Investigating,
    startDate: '2026-10-14T08:00:00Z',
    endDate: null,
    description: 'Investigating',
    createdAt: '2026-10-14T08:00:00Z',
  },
  {
    id: 2,
    title: 'Router upgrade',
    type: NetworkIssueType.Other,
    server: null,
    priority: NetworkIssuePriority.Low,
    status: NetworkIssueStatus.Resolved,
    startDate: '2026-10-10T08:00:00Z',
    endDate: '2026-10-10T10:00:00Z',
    description: 'Done',
    createdAt: '2026-10-10T08:00:00Z',
  },
]

/** The categories the knowledgebase screen offers. */
const categories: KbCategory[] = [
  { id: 3, name: 'Billing', description: '', isHidden: false, parentCategoryId: null, articleCount: 1, slug: 'billing' },
  { id: 4, name: 'Hosting', description: '', isHidden: false, parentCategoryId: null, articleCount: 1, slug: 'hosting' },
]

/** Every article the stub holds. */
const articles: KbArticle[] = [
  { id: 11, slug: 'how-to-pay', categoryId: 3, categoryName: 'Billing', isPublished: true, translations: { en: { title: 'How to pay', content: 'Use a card' } } },
  { id: 12, slug: 'set-up-dns', categoryId: 4, categoryName: 'Hosting', isPublished: false, translations: { en: { title: 'Set up DNS', content: 'Add a record' } } },
]

/**
 * Builds a page answer around some entries.
 *
 * @param items - The entries of the page.
 * @returns The page as the API answers it.
 */
const pageOf = <T>(items: T[]): PaginatedList<T> => ({ items, pageNumber: 1, pageSize: 20, totalCount: items.length, totalPages: 1 })

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

/**
 * Stubs the announcement list so it narrows by the search and the published state the same way the real API does.
 *
 * @returns The stubbed route.
 */
const announcementRoute = (): StubRoute => ({
  method: HttpMethod.Get,
  path: '/api/announcements',
  respond: (call) => {
    const query = new URLSearchParams(call.search)
    const term = query.get('search')?.toLowerCase() ?? ''
    const published = query.get('isPublished')
    return {
      body: pageOf(
        announcements.filter(
          (entry) => `${entry.title} ${entry.content}`.toLowerCase().includes(term) && (published === null || String(entry.isPublished) === published),
        ),
      ),
    }
  },
})

test('the announcements list sends its search to the API and shows only the matching announcement', async ({ page }) => {
  const api = await stubApi(page, { signedIn: true, routes: [announcementRoute()] })

  await page.goto('/support/announcements')
  await expect(page.getByTestId('announcements-table')).toContainText('Scheduled maintenance')
  await expect(page.getByTestId('announcements-table')).toContainText('New data center')

  await fieldOf(page, 'announcements-search').fill('data center')
  await expect(page).toHaveURL(/search=data/)

  await expect(page.getByTestId('announcements-table')).not.toContainText('Scheduled maintenance')
  await expect(page.getByTestId('announcements-table')).toContainText('New data center')
  expect(api.callsTo(HttpMethod.Get, '/api/announcements').at(-1)?.search).toContain('search=data')
})

test('the published filter of the announcements is sent to the API and a chip removes it', async ({ page }) => {
  const api = await stubApi(page, { signedIn: true, routes: [announcementRoute()] })

  await page.goto('/support/announcements')
  await expect(page.getByTestId('announcements-table')).toContainText('New data center')

  await page.getByTestId('announcements-filter').click()
  await chooseOption(page, 'announcements-filter-published', 'Draft')
  await page.getByTestId('filter-apply').click()

  await expect(page).toHaveURL(/published=draft/)
  await expect(page.getByTestId('filter-chip-published')).toContainText('Draft')
  await expect(page.getByTestId('announcements-table')).not.toContainText('Scheduled maintenance')
  expect(api.callsTo(HttpMethod.Get, '/api/announcements').at(-1)?.search).toContain('isPublished=false')

  await page.getByTestId('filter-chip-remove-published').click()

  await expect(page.getByTestId('filter-chip-published')).toHaveCount(0)
  await expect(page.getByTestId('announcements-table')).toContainText('Scheduled maintenance')
  expect(api.callsTo(HttpMethod.Get, '/api/announcements').at(-1)?.search).not.toContain('isPublished=')
})

test('the network issues list has filters instead of status tabs and sends them to the API', async ({ page }) => {
  const api = await stubApi(page, {
    signedIn: true,
    routes: [
      {
        method: HttpMethod.Get,
        path: '/api/network-issues',
        respond: (call) => {
          const query = new URLSearchParams(call.search)
          const term = query.get('search')?.toLowerCase() ?? ''
          return {
            body: pageOf(
              issues.filter(
                (entry) =>
                  `${entry.title} ${entry.server ?? ''} ${entry.description}`.toLowerCase().includes(term)
                  && (query.get('status') === null || entry.status === query.get('status'))
                  && (query.get('priority') === null || entry.priority === query.get('priority')),
              ),
            ),
          }
        },
      },
    ],
  })

  await page.goto('/support/network-issues')
  await expect(page.getByTestId('network-issues-table')).toContainText('Packet loss')
  await expect(page.getByTestId('network-issues-table')).toContainText('Router upgrade')
  await expect(page.getByTestId('network-issues-tab-Resolved')).toHaveCount(0)

  await page.getByTestId('network-issues-filter').click()
  await chooseOption(page, 'network-issues-filter-status', 'Resolved')
  await chooseOption(page, 'network-issues-filter-priority', 'Low')
  await page.getByTestId('filter-apply').click()

  await expect(page).toHaveURL(/status=Resolved/)
  await expect(page).toHaveURL(/priority=Low/)
  await expect(page.getByTestId('filter-chip-status')).toContainText('Resolved')
  await expect(page.getByTestId('filter-chip-priority')).toContainText('Low')
  await expect(page.getByTestId('network-issues-table')).not.toContainText('Packet loss')
  await expect(page.getByTestId('network-issues-table')).toContainText('Router upgrade')
  const sent = api.callsTo(HttpMethod.Get, '/api/network-issues').at(-1)?.search ?? ''
  expect(sent).toContain('status=Resolved')
  expect(sent).toContain('priority=Low')

  await page.getByTestId('filter-chip-remove-status').click()

  await expect(page.getByTestId('filter-chip-status')).toHaveCount(0)
  expect(api.callsTo(HttpMethod.Get, '/api/network-issues').at(-1)?.search).not.toContain('status=')
})

test('a network issues link with a search and a type opens the filtered list', async ({ page }) => {
  const api = await stubApi(page, { signedIn: true, routes: [json(HttpMethod.Get, '/api/network-issues', pageOf([issues[0] as NetworkIssue]))] })

  await page.goto('/support/network-issues?search=loss&type=Server')

  await expect(page.getByTestId('filter-chip-type')).toContainText('Server')
  await expect(page.getByTestId('network-issues-table')).toContainText('Packet loss')
  const sent = api.callsTo(HttpMethod.Get, '/api/network-issues')[0]?.search ?? ''
  expect(sent).toContain('search=loss')
  expect(sent).toContain('type=Server')
})

test('the knowledgebase article list sends its search and its category filter to the API', async ({ page }) => {
  const api = await stubApi(page, {
    signedIn: true,
    routes: [
      { method: HttpMethod.Get, path: '/api/admin/knowledgebase/categories', respond: () => ({ body: categories }) },
      {
        method: HttpMethod.Get,
        path: '/api/admin/knowledgebase',
        respond: (call) => {
          const query = new URLSearchParams(call.search)
          const term = query.get('search')?.toLowerCase() ?? ''
          return {
            body: pageOf(
              articles.filter(
                (entry) =>
                  (entry.translations.en?.title ?? '').toLowerCase().includes(term)
                  && (query.get('categoryId') === null || String(entry.categoryId) === query.get('categoryId')),
              ),
            ),
          }
        },
      },
    ],
  })

  await page.goto('/support/knowledgebase')
  await page.getByTestId('knowledgebase-tab-article').click()
  await expect(page.getByTestId('knowledgebase-articles')).toContainText('How to pay')
  await expect(page.getByTestId('knowledgebase-articles')).toContainText('Set up DNS')

  await fieldOf(page, 'knowledgebase-articles-search').fill('dns')
  await expect(page).toHaveURL(/search=dns/)
  await expect(page.getByTestId('knowledgebase-articles')).not.toContainText('How to pay')
  expect(api.callsTo(HttpMethod.Get, '/api/admin/knowledgebase').at(-1)?.search).toContain('search=dns')

  await fieldOf(page, 'knowledgebase-articles-search').fill('')
  await expect(page.getByTestId('knowledgebase-articles')).toContainText('How to pay')

  await page.getByTestId('knowledgebase-articles-filter').click()
  await chooseOption(page, 'knowledgebase-articles-filter-category', 'Billing')
  await page.getByTestId('filter-apply').click()

  await expect(page).toHaveURL(/categoryId=3/)
  await expect(page.getByTestId('filter-chip-categoryId')).toContainText('Billing')
  await expect(page.getByTestId('knowledgebase-articles')).not.toContainText('Set up DNS')
  expect(api.callsTo(HttpMethod.Get, '/api/admin/knowledgebase').at(-1)?.search).toContain('categoryId=3')
})
