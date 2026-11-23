/**
 * Gateway log flow: the screen lists what the API returns, sends the search to the API and keeps it in the address,
 * sends the result and the period as bare days from the filter dialog, shows each applied filter as a chip that is
 * removed from its own button, and a link with filters opens the filtered log.
 */
import { HttpMethod } from '../src/types/http'
import { expect, test } from '@playwright/test'
import type { Page } from '@playwright/test'
import { gatewayLogPage } from './fixtures/gatewayLog'
import { fieldOf } from './support/locators'
import { json, stubApi } from './support/stubApi'

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

test('the gateway log lists the API entries and sends the search to the API', async ({ page }) => {
  const api = await stubApi(page, {
    signedIn: true,
    routes: [json(HttpMethod.Get, '/api/gateway-logs', gatewayLogPage)],
  })

  await page.goto('/billing/gateway-log')
  await expect(page.getByTestId('billing-gateway-log-table')).toContainText('Invoice #9 order ord-9')
  await expect(page.getByTestId('billing-gateway-log-table')).toContainText('Invoice #4 ref ref-4')

  // The search box writes to the address after a pause and the log reloads from it.
  await fieldOf(page, 'billing-gateway-log-search').fill('ref-4')
  await expect(page).toHaveURL(/search=ref-4/)

  await expect.poll(() => api.callsTo(HttpMethod.Get, '/api/gateway-logs').length).toBeGreaterThan(1)
  expect(api.callsTo(HttpMethod.Get, '/api/gateway-logs').at(-1)?.search).toContain('search=ref-4')
})

test('the result filter is sent to the API and a chip removes it again', async ({ page }) => {
  const api = await stubApi(page, {
    signedIn: true,
    routes: [json(HttpMethod.Get, '/api/gateway-logs', gatewayLogPage)],
  })

  await page.goto('/billing/gateway-log')
  await expect(page.getByTestId('billing-gateway-log-table')).toContainText('Invoice #9 order ord-9')

  await page.getByTestId('billing-gateway-log-filter').click()
  await chooseOption(page, 'billing-gateway-log-filter-result', 'Pending')
  await page.getByTestId('filter-apply').click()

  await expect(page).toHaveURL(/result=Pending/)
  await expect(page.getByTestId('filter-chip-result')).toContainText('Pending')
  expect(api.callsTo(HttpMethod.Get, '/api/gateway-logs').at(-1)?.search).toContain('result=Pending')

  await page.getByTestId('filter-chip-remove-result').click()

  await expect(page.getByTestId('filter-chip-result')).toHaveCount(0)
  expect(api.callsTo(HttpMethod.Get, '/api/gateway-logs').at(-1)?.search).not.toContain('result=')
})

test('a link with a period opens the log for those days and sends them as bare days', async ({ page }) => {
  const api = await stubApi(page, {
    signedIn: true,
    routes: [json(HttpMethod.Get, '/api/gateway-logs', gatewayLogPage)],
  })

  await page.goto('/billing/gateway-log?from=2026-10-13&to=2026-10-14')

  await expect(page.getByTestId('filter-chip-from')).toBeVisible()
  await expect(page.getByTestId('filter-chip-to')).toBeVisible()
  const sent = api.callsTo(HttpMethod.Get, '/api/gateway-logs')[0]?.search ?? ''
  expect(sent).toContain('from=2026-10-13')
  expect(sent).toContain('to=2026-10-14')
  expect(sent).not.toContain('T00')
})
