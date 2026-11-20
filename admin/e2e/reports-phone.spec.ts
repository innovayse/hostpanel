/**
 * A report at phone width: its table is a list of record cards (first column as the heading, the other columns as captioned fields),
 * with no column header row and no table markup, and nothing scrolls sideways.
 */
import { HttpMethod } from '../src/types/http'
import { expect, test } from '@playwright/test'
import { topClients } from './fixtures/reports'
import { json, stubApi } from './support/stubApi'

test.use({ viewport: { width: 390, height: 800 } })

test('a report table is a list of cards on a phone', async ({ page }) => {
  await stubApi(page, { signedIn: true, routes: [json(HttpMethod.Get, '/api/reports/top-clients-by-income', topClients)] })

  await page.goto('/reports/top-clients')

  const cards = page.getByTestId('report-table-row')
  await expect(cards).toHaveCount(topClients.length)
  await expect(cards.first()).toHaveAttribute('role', 'listitem')
  await expect(cards.first()).toContainText(topClients[0]?.clientName ?? '')
  await expect(page.locator('table')).toHaveCount(0)
  await expect(page.locator('th')).toHaveCount(0)

  const overflow = await page.evaluate(() => document.documentElement.scrollWidth - document.documentElement.clientWidth)
  expect(overflow).toBeLessThanOrEqual(0)
})
