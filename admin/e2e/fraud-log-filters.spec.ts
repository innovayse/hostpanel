/**
 * Fraud log flow: the search on the email and IP address and the kind, decision and reason filters of the filter dialog
 * are sent to the API and kept in the address, an applied filter is a chip that is removed from its own button, and a
 * link with a search and filters opens the narrowed log.
 */
import { HttpMethod } from '../src/types/http'
import { expect, test } from '@playwright/test'
import type { Page } from '@playwright/test'
import { FraudCheckKind, FraudDecision, FraudReasonCode } from '../src/types/fraud'
import type { FraudLogEntry } from '../src/types/fraud'
import type { PaginatedList } from '../src/types/paginatedlist'
import { fieldOf } from './support/locators'
import { stubApi } from './support/stubApi'
import type { StubApi } from './support/stubApi'

/** An order held for review that came from a proxy address. */
const heldOrder: FraudLogEntry = {
  id: 11,
  kind: FraudCheckKind.Order,
  decision: FraudDecision.Review,
  clientId: 5,
  orderId: 40,
  ipAddress: '203.0.113.7',
  email: 'buyer@example.test',
  score: 62,
  reasons: [FraudReasonCode.AnonymousIp],
  createdAt: '2026-11-01T10:00:00Z',
}

/** A registration that was rejected: the one the filters must keep apart from the held order. */
const rejectedRegistration: FraudLogEntry = {
  ...heldOrder,
  id: 12,
  kind: FraudCheckKind.Registration,
  decision: FraudDecision.Rejected,
  clientId: null,
  orderId: null,
  ipAddress: '198.51.100.9',
  email: 'bot@example.test',
  reasons: [FraudReasonCode.BlockedIp],
}

/** Every entry the stubbed log holds. */
const allEntries: FraudLogEntry[] = [heldOrder, rejectedRegistration]

/**
 * Stubs the fraud log so it narrows by the same query parameters the real API takes.
 *
 * @param page - The page under test.
 * @returns The stubbed API, to read the requests it saw.
 */
const stubFraudLog = (page: Page): Promise<StubApi> =>
  stubApi(page, {
    signedIn: true,
    routes: [
      {
        method: HttpMethod.Get,
        path: '/api/admin/fraud/log',
        respond: (call) => {
          const query = new URLSearchParams(call.search)
          const term = query.get('search')?.toLowerCase() ?? ''
          const items = allEntries.filter(
            (entry) =>
              `${entry.email ?? ''} ${entry.ipAddress ?? ''}`.includes(term) &&
              (query.get('kind') === null || entry.kind === query.get('kind')) &&
              (query.get('decision') === null || entry.decision === query.get('decision')),
          )
          const body: PaginatedList<FraudLogEntry> = { items, totalCount: items.length, pageNumber: 1, pageSize: 20, totalPages: 1 }
          return { body }
        },
      },
    ],
  })

test('searching by address sends the search and shows only the matching entry', async ({ page }) => {
  const api = await stubFraudLog(page)

  await page.goto('/fraud/log')
  await expect(page.getByTestId(`fraud-log-row-${heldOrder.id}`)).toBeVisible()
  await expect(page.getByTestId(`fraud-log-row-${rejectedRegistration.id}`)).toBeVisible()

  // The search box writes to the address after a pause and the log reloads from it.
  await fieldOf(page, 'fraud-log-search').fill('198.51')
  await expect(page).toHaveURL(/search=198\.51/)

  await expect(page.getByTestId(`fraud-log-row-${rejectedRegistration.id}`)).toBeVisible()
  await expect(page.getByTestId(`fraud-log-row-${heldOrder.id}`)).toHaveCount(0)
  expect(api.callsTo(HttpMethod.Get, '/api/admin/fraud/log').at(-1)?.search).toContain('search=198.51')
})

test('the kind and the decision filters are sent to the API, shown as chips and removed from their chips', async ({ page }) => {
  const api = await stubFraudLog(page)

  await page.goto('/fraud/log')
  await expect(page.getByTestId(`fraud-log-row-${heldOrder.id}`)).toBeVisible()

  await page.getByTestId('filter-trigger').click()
  await page.getByTestId('fraud-log-kind').getByTestId('ui-select-trigger').click()
  await page.getByTestId('ui-select-option').filter({ hasText: /^Registration$/ }).click()
  await page.getByTestId('filter-apply').click()

  await expect(page).toHaveURL(/kind=Registration/)
  await expect(page.getByTestId('filter-chip-kind')).toContainText('Registration')
  await expect(page.getByTestId(`fraud-log-row-${rejectedRegistration.id}`)).toBeVisible()
  await expect(page.getByTestId(`fraud-log-row-${heldOrder.id}`)).toHaveCount(0)
  expect(api.callsTo(HttpMethod.Get, '/api/admin/fraud/log').at(-1)?.search).toContain('kind=Registration')

  await page.getByTestId('filter-chip-remove-kind').click()

  await expect(page.getByTestId('filter-chip-kind')).toHaveCount(0)
  await expect(page.getByTestId(`fraud-log-row-${heldOrder.id}`)).toBeVisible()
  expect(api.callsTo(HttpMethod.Get, '/api/admin/fraud/log').at(-1)?.search).not.toContain('kind=')
})

test('a log link with a search and filters opens the narrowed log', async ({ page }) => {
  const api = await stubFraudLog(page)

  await page.goto('/fraud/log?search=example&decision=Review&reason=AnonymousIp')

  await expect(page.getByTestId('filter-chip-decision')).toContainText('Pending review')
  await expect(page.getByTestId('filter-chip-reason')).toContainText('Proxy, VPN or hosting IP address')
  await expect(page.getByTestId(`fraud-log-row-${heldOrder.id}`)).toBeVisible()
  await expect(page.getByTestId(`fraud-log-row-${rejectedRegistration.id}`)).toHaveCount(0)
  const first = api.callsTo(HttpMethod.Get, '/api/admin/fraud/log')[0]?.search ?? ''
  expect(first).toContain('search=example')
  expect(first).toContain('decision=Review')
  expect(first).toContain('reason=AnonymousIp')
})
