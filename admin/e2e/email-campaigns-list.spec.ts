/**
 * Email campaigns list flow: the subject search, the status filter of the filter dialog and the page are sent to the
 * API and kept in the address, and an applied filter is a chip that is removed from its own button.
 */
import { HttpMethod } from '../src/types/http'
import { expect, test } from '@playwright/test'
import type { Page } from '@playwright/test'
import { CampaignStatus } from '../src/types/emailcampaign'
import type { EmailCampaignListItem } from '../src/types/emailcampaign'
import type { PaginatedList } from '../src/types/paginatedlist'
import { fieldOf } from './support/locators'
import { stubApi } from './support/stubApi'
import type { StubApi } from './support/stubApi'

/** The list endpoint the screen reads. */
const LIST_PATH = '/api/admin/email-campaigns'

/**
 * Builds a list row.
 *
 * @param id - The campaign id.
 * @param subject - The mail subject.
 * @param status - The lifecycle status.
 * @returns The row.
 */
const campaignRow = (id: number, subject: string, status: CampaignStatus): EmailCampaignListItem => ({
  id,
  subject,
  status,
  scheduledAt: null,
  sentAt: null,
  createdAt: '2026-10-01T10:30:00Z',
  stats: {
    total: 0,
    pending: 0,
    sent: 0,
    failed: 0,
    unsubscribed: 0,
    opened: 0,
    uniqueOpened: 0,
    clicked: 0,
    uniqueClicked: 0,
    openRate: 0,
    clickRate: 0,
  },
})

/** A draft campaign. */
const draftRow = campaignRow(3, 'Spring offer', CampaignStatus.Draft)

/** A finished campaign, the one the status filter must keep. */
const sentRow = campaignRow(4, 'Maintenance notice', CampaignStatus.Sent)

/** Every campaign the stubbed list holds. */
const allRows: EmailCampaignListItem[] = [draftRow, sentRow]

/**
 * Stubs the campaign list so it narrows by the same query parameters the real API takes.
 *
 * @param page - The page under test.
 * @returns The stubbed API, to read the requests it saw.
 */
const stubCampaignList = (page: Page): Promise<StubApi> =>
  stubApi(page, {
    signedIn: true,
    routes: [
      {
        method: HttpMethod.Get,
        path: LIST_PATH,
        respond: (call) => {
          const query = new URLSearchParams(call.search)
          const term = query.get('search')?.toLowerCase() ?? ''
          const status = query.get('status')
          const items = allRows.filter(
            (row) => row.subject.toLowerCase().includes(term) && (status === null || row.status === status),
          )
          const body: PaginatedList<EmailCampaignListItem> = {
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

test('searching by subject sends the search and shows only the matching campaign', async ({ page }) => {
  const api = await stubCampaignList(page)

  await page.goto('/support/email-campaigns')
  await expect(page.getByTestId('email-campaigns-row-3')).toBeVisible()
  await expect(page.getByTestId('email-campaigns-row-4')).toBeVisible()

  // The search box writes to the address after a pause and the list reloads from it.
  await fieldOf(page, 'email-campaigns-search').fill('maintenance')
  await expect(page).toHaveURL(/search=maintenance/)

  await expect(page.getByTestId('email-campaigns-row-4')).toBeVisible()
  await expect(page.getByTestId('email-campaigns-row-3')).toHaveCount(0)
  expect(api.callsTo(HttpMethod.Get, LIST_PATH).at(-1)?.search).toContain('search=maintenance')
})

test('the status filter is sent to the API, shown as a chip and removed from its chip', async ({ page }) => {
  const api = await stubCampaignList(page)

  await page.goto('/support/email-campaigns')
  await expect(page.getByTestId('email-campaigns-row-3')).toBeVisible()

  await page.getByTestId('filter-trigger').click()
  await page.getByTestId('email-campaigns-filter-status').getByTestId('ui-select-trigger').click()
  await page.getByTestId('ui-select-option').filter({ hasText: /^Sent$/ }).click()
  await page.getByTestId('filter-apply').click()

  await expect(page).toHaveURL(/status=Sent/)
  await expect(page.getByTestId('filter-chip-status')).toContainText('Sent')
  await expect(page.getByTestId('email-campaigns-row-3')).toHaveCount(0)
  await expect(page.getByTestId('email-campaigns-row-4')).toBeVisible()
  expect(api.callsTo(HttpMethod.Get, LIST_PATH).at(-1)?.search).toContain('status=Sent')

  await page.getByTestId('filter-chip-remove-status').click()

  await expect(page.getByTestId('filter-chip-status')).toHaveCount(0)
  await expect(page.getByTestId('email-campaigns-row-3')).toBeVisible()
  expect(api.callsTo(HttpMethod.Get, LIST_PATH).at(-1)?.search).not.toContain('status=')
})

test('a list link with a filter opens the filtered list', async ({ page }) => {
  const api = await stubCampaignList(page)

  await page.goto('/support/email-campaigns?status=Draft')

  await expect(page.getByTestId('filter-chip-status')).toContainText('Draft')
  await expect(page.getByTestId('email-campaigns-row-3')).toBeVisible()
  await expect(page.getByTestId('email-campaigns-row-4')).toHaveCount(0)
  expect(api.callsTo(HttpMethod.Get, LIST_PATH)[0]?.search).toContain('status=Draft')
})
