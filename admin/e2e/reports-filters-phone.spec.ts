/**
 * The filters of a query report at phone width: the inline filter card folds into one Filters button that carries the
 * count of the filters in force and opens the Filters sheet with the date range, the column picker and the advanced
 * filters, whose Apply runs the report; the advanced filters typed before are still there when the sheet opens again.
 */
import { HttpMethod } from '../src/types/http'
import { expect, test } from '@playwright/test'
import { ClientStatus } from '../src/types/client'
import type { ReportClientRow } from '../src/types/reportclientrow'
import { fieldOf } from './support/locators'
import { json, stubApi } from './support/stubApi'

test.use({ viewport: { width: 390, height: 800 } })

/** The one client the stubbed report returns. */
const reportClient: ReportClientRow = {
  id: 7,
  firstName: 'Ada',
  lastName: 'Lovelace',
  companyName: null,
  email: 'ada@example.test',
  address: { address1: null, address2: null, city: null, state: null, postCode: null, country: null },
  phone: null,
  currency: 'USD',
  creditBalance: 0,
  status: ClientStatus.Active,
  createdAt: '2026-01-15T10:00:00Z',
  notes: null,
}

/** The page of the clients report. */
const reportPage = { items: [reportClient], pageNumber: 1, pageSize: 50, totalCount: 1, totalPages: 1 }

test('the report filters are a Filters sheet with the count on its button, Apply runs the report and the advanced rows stay', async ({ page }) => {
  const api = await stubApi(page, { signedIn: true, routes: [json(HttpMethod.Get, '/api/reports/clients', reportPage)] })

  await page.goto('/reports/clients')
  await expect(page.getByTestId('report-filter-trigger')).toBeVisible()
  // The inline card is gone: its Generate button lives in the sheet as Apply.
  await expect(page.getByTestId('report-generate')).toHaveCount(0)
  // The creation-date range starts on its default preset, so one filter is in force.
  await expect(page.getByTestId('filter-trigger-count')).toHaveText('1')
  const before = api.callsTo(HttpMethod.Get, '/api/reports/clients').length

  await page.getByTestId('report-filter-trigger').click()
  await expect(page.getByTestId('drawer-grip')).toBeVisible()
  await expect(page.getByRole('heading', { name: 'Filters' })).toBeVisible()
  await expect(page.getByTestId('field-selector-trigger')).toBeVisible()
  await expect(page.getByTestId('report-range-created')).toBeVisible()
  await fieldOf(page, 'advanced-filters-value-0').fill('Active')
  await page.getByTestId('filter-apply').click()

  await expect(page.getByTestId('dialog-panel')).toHaveCount(0)
  await expect(page.getByTestId('filter-trigger-count')).toHaveText('2')
  await expect.poll(() => api.callsTo(HttpMethod.Get, '/api/reports/clients').length).toBeGreaterThan(before)

  // The sheet is built again on every opening, and still shows what is in force.
  await page.getByTestId('report-filter-trigger').click()
  await expect(fieldOf(page, 'advanced-filters-value-0')).toHaveValue('Active')
})
