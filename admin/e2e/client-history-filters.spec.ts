/**
 * Client history flows: on the activity log and the e-mail log of a client's profile, the search, the period and the
 * outcome are sent to the API and kept in the address, each applied filter is a chip that is removed from its own
 * button, and a link with filters opens the filtered log.
 */
import { HttpMethod } from '../src/types/http'
import { expect, test } from '@playwright/test'
import type { Page } from '@playwright/test'
import type { ActivityLog } from '../src/types/activitylog'
import type { EmailLog } from '../src/types/emaillog'
import { EmailSendFailure } from '../src/types/emaillog'
import type { PaginatedList } from '../src/types/paginatedlist'
import { adaDetail } from './fixtures/clients'
import { fieldOf } from './support/locators'
import { json, stubApi } from './support/stubApi'
import type { StubApi, StubRoute } from './support/stubApi'

/** Every entry of the activity log the stub holds. */
const activity: ActivityLog[] = [
  { id: 1, description: 'Updated the profile', adminName: 'Ada Admin', adminEmail: 'ada@example.test', ipAddress: '10.0.0.5', createdAt: '2026-10-14T10:00:00Z' },
  { id: 2, description: 'Added a transaction', adminName: 'Grace Admin', adminEmail: 'grace@example.test', ipAddress: '192.168.1.9', createdAt: '2026-10-13T09:00:00Z' },
]

/** Every entry of the e-mail log the stub holds. */
const emails: EmailLog[] = [
  { id: 1, to: 'ada@example.test', subject: 'Welcome aboard', sentAt: '2026-10-14T10:00:00Z', success: true, failure: null },
  { id: 2, to: 'ada@example.test', subject: 'Invoice due', sentAt: '2026-10-13T09:00:00Z', success: false, failure: EmailSendFailure.RecipientRejected },
]

/**
 * Builds a page answer around some entries.
 *
 * @param items - The entries of the page.
 * @returns The page as the API answers it.
 */
const pageOf = <T>(items: T[]): PaginatedList<T> => ({ items, pageNumber: 1, pageSize: 25, totalCount: items.length, totalPages: 1 })

/**
 * Stubs the activity log so it narrows by the search the same way the real API does.
 *
 * @returns The stubbed route.
 */
const activityRoute = (): StubRoute => ({
  method: HttpMethod.Get,
  path: '/api/clients/7/activity-logs',
  respond: (call) => {
    const term = new URLSearchParams(call.search).get('search')?.toLowerCase() ?? ''
    return { body: pageOf(activity.filter((entry) => `${entry.description} ${entry.adminName} ${entry.adminEmail} ${entry.ipAddress}`.toLowerCase().includes(term))) }
  },
})

/**
 * Stubs the e-mail log so it narrows by the search and the outcome the same way the real API does.
 *
 * @returns The stubbed route.
 */
const emailRoute = (): StubRoute => ({
  method: HttpMethod.Get,
  path: '/api/admin/email-logs/client/7',
  respond: (call) => {
    const query = new URLSearchParams(call.search)
    const term = query.get('search')?.toLowerCase() ?? ''
    const success = query.get('success')
    return {
      body: pageOf(
        emails.filter(
          (entry) => `${entry.subject} ${entry.to}`.toLowerCase().includes(term) && (success === null || String(entry.success) === success),
        ),
      ),
    }
  },
})

/**
 * Stubs a client's profile with one history endpoint.
 *
 * @param page - The page under test.
 * @param route - The history endpoint.
 * @returns The stubbed API, to read the requests it saw.
 */
const stubHistory = (page: Page, route: StubRoute): Promise<StubApi> =>
  stubApi(page, { signedIn: true, routes: [json(HttpMethod.Get, '/api/clients/7', adaDetail), route] })

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

test('the activity log sends its search to the API, keeps it in the address and shows only the matching entry', async ({ page }) => {
  const api = await stubHistory(page, activityRoute())

  await page.goto('/clients/7/log')
  await expect(page.getByTestId('client-log-table')).toContainText('Updated the profile')
  await expect(page.getByTestId('client-log-table')).toContainText('Added a transaction')

  // The search box writes to the address after a pause and the log reloads from it.
  await fieldOf(page, 'client-log-search').fill('192.168')
  await expect(page).toHaveURL(/search=192\.168/)

  await expect(page.getByTestId('client-log-table')).not.toContainText('Updated the profile')
  await expect(page.getByTestId('client-log-table')).toContainText('Added a transaction')
  expect(api.callsTo(HttpMethod.Get, '/api/clients/7/activity-logs').at(-1)?.search).toContain('search=192.168')
})

test('an activity log link with a period shows it as chips, sends bare days, and a chip removes its day', async ({ page }) => {
  const api = await stubHistory(page, activityRoute())

  await page.goto('/clients/7/log?from=2026-10-13&to=2026-10-14')

  await expect(page.getByTestId('filter-chip-from')).toBeVisible()
  await expect(page.getByTestId('filter-chip-to')).toBeVisible()
  const sent = api.callsTo(HttpMethod.Get, '/api/clients/7/activity-logs')[0]?.search ?? ''
  expect(sent).toContain('from=2026-10-13')
  expect(sent).toContain('to=2026-10-14')

  await page.getByTestId('filter-chip-remove-to').click()

  await expect(page.getByTestId('filter-chip-to')).toHaveCount(0)
  expect(api.callsTo(HttpMethod.Get, '/api/clients/7/activity-logs').at(-1)?.search).not.toContain('to=')
})

test('the e-mail log sends its search to the API and shows only the matching entry', async ({ page }) => {
  const api = await stubHistory(page, emailRoute())

  await page.goto('/clients/7/emails')
  await expect(page.getByTestId('client-emails-table')).toContainText('Welcome aboard')
  await expect(page.getByTestId('client-emails-table')).toContainText('Invoice due')

  await fieldOf(page, 'client-emails-search').fill('invoice')
  await expect(page).toHaveURL(/search=invoice/)

  await expect(page.getByTestId('client-emails-table')).not.toContainText('Welcome aboard')
  await expect(page.getByTestId('client-emails-table')).toContainText('Invoice due')
  expect(api.callsTo(HttpMethod.Get, '/api/admin/email-logs/client/7').at(-1)?.search).toContain('search=invoice')
})

test('the outcome filter of the e-mail log is sent as the success switch and a chip removes it', async ({ page }) => {
  const api = await stubHistory(page, emailRoute())

  await page.goto('/clients/7/emails')
  await expect(page.getByTestId('client-emails-table')).toContainText('Welcome aboard')

  await page.getByTestId('client-emails-filter').click()
  await chooseOption(page, 'client-emails-filter-outcome', 'Not delivered')
  await page.getByTestId('filter-apply').click()

  await expect(page).toHaveURL(/outcome=failed/)
  await expect(page.getByTestId('filter-chip-outcome')).toContainText('Not delivered')
  await expect(page.getByTestId('client-emails-table')).not.toContainText('Welcome aboard')
  await expect(page.getByTestId('client-emails-table')).toContainText('Invoice due')
  expect(api.callsTo(HttpMethod.Get, '/api/admin/email-logs/client/7').at(-1)?.search).toContain('success=false')

  await page.getByTestId('filter-chip-remove-outcome').click()

  await expect(page.getByTestId('filter-chip-outcome')).toHaveCount(0)
  await expect(page.getByTestId('client-emails-table')).toContainText('Welcome aboard')
  expect(api.callsTo(HttpMethod.Get, '/api/admin/email-logs/client/7').at(-1)?.search).not.toContain('success=')
})
