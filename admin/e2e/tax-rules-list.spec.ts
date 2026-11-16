/**
 * Tax rules list flow: the search, the enabled filter of the filter dialog and the page are sent to the API and kept
 * in the address, and an applied filter is a chip that is removed from its own button.
 */
import { HttpMethod } from '../src/types/http'
import { expect, test } from '@playwright/test'
import type { Page } from '@playwright/test'
import type { PaginatedList } from '../src/types/paginatedlist'
import type { TaxRuleDto } from '../src/types/taxrule'
import { fieldOf } from './support/locators'
import { stubApi } from './support/stubApi'
import type { StubApi } from './support/stubApi'

/** The list endpoint the screen reads. */
const LIST_PATH = '/api/admin/tax-rules'

/** A country-wide rule that new invoices use. */
const germany: TaxRuleDto = { id: 1, name: 'Germany VAT', country: 'DE', region: null, ratePercent: 19, isEnabled: true }

/** A switched-off regional rule, the one the enabled filter must keep. */
const bavaria: TaxRuleDto = { id: 2, name: 'Bavaria VAT', country: 'DE', region: 'Bavaria', ratePercent: 7.5, isEnabled: false }

/** Every rule the stubbed list holds. */
const allRules: TaxRuleDto[] = [germany, bavaria]

/**
 * Stubs the tax rule list so it narrows by the same query parameters the real API takes.
 *
 * @param page - The page under test.
 * @returns The stubbed API, to read the requests it saw.
 */
const stubRuleList = (page: Page): Promise<StubApi> =>
  stubApi(page, {
    signedIn: true,
    routes: [
      {
        method: HttpMethod.Get,
        path: LIST_PATH,
        respond: (call) => {
          const query = new URLSearchParams(call.search)
          const term = query.get('search')?.toLowerCase() ?? ''
          const enabled = query.get('enabled')
          const items = allRules.filter(
            (rule) =>
              `${rule.name} ${rule.country} ${rule.region ?? ''}`.toLowerCase().includes(term)
              && (enabled === null || String(rule.isEnabled) === enabled),
          )
          const body: PaginatedList<TaxRuleDto> = {
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

test('searching sends the search and shows only the matching rule', async ({ page }) => {
  const api = await stubRuleList(page)

  await page.goto('/settings/tax-rules')
  await expect(page.getByTestId('tax-rule-row-1')).toBeVisible()
  await expect(page.getByTestId('tax-rule-row-2')).toBeVisible()

  // The search box writes to the address after a pause and the list reloads from it.
  await fieldOf(page, 'tax-rules-search').fill('bavaria')
  await expect(page).toHaveURL(/search=bavaria/)

  await expect(page.getByTestId('tax-rule-row-2')).toBeVisible()
  await expect(page.getByTestId('tax-rule-row-1')).toHaveCount(0)
  expect(api.callsTo(HttpMethod.Get, LIST_PATH).at(-1)?.search).toContain('search=bavaria')
})

test('the enabled filter is sent to the API, shown as a chip and removed from its chip', async ({ page }) => {
  const api = await stubRuleList(page)

  await page.goto('/settings/tax-rules')
  await expect(page.getByTestId('tax-rule-row-1')).toBeVisible()

  await page.getByTestId('filter-trigger').click()
  await page.getByTestId('tax-rules-filter-enabled').getByTestId('ui-select-trigger').click()
  await page.getByTestId('ui-select-option').filter({ hasText: /^Disabled$/ }).click()
  await page.getByTestId('filter-apply').click()

  await expect(page).toHaveURL(/enabled=disabled/)
  await expect(page.getByTestId('filter-chip-enabled')).toContainText('Disabled')
  await expect(page.getByTestId('tax-rule-row-1')).toHaveCount(0)
  await expect(page.getByTestId('tax-rule-row-2')).toBeVisible()
  expect(api.callsTo(HttpMethod.Get, LIST_PATH).at(-1)?.search).toContain('enabled=false')

  await page.getByTestId('filter-chip-remove-enabled').click()

  await expect(page.getByTestId('filter-chip-enabled')).toHaveCount(0)
  await expect(page.getByTestId('tax-rule-row-1')).toBeVisible()
  expect(api.callsTo(HttpMethod.Get, LIST_PATH).at(-1)?.search).not.toContain('enabled=')
})

test('a list link with a filter opens the filtered list', async ({ page }) => {
  const api = await stubRuleList(page)

  await page.goto('/settings/tax-rules?enabled=enabled')

  await expect(page.getByTestId('filter-chip-enabled')).toContainText('Enabled')
  await expect(page.getByTestId('tax-rule-row-1')).toBeVisible()
  await expect(page.getByTestId('tax-rule-row-2')).toHaveCount(0)
  expect(api.callsTo(HttpMethod.Get, LIST_PATH)[0]?.search).toContain('enabled=true')
})
