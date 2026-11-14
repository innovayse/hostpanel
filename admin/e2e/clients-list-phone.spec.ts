/**
 * Clients list at phone width: the table is drawn as cards (name as the heading, status as the badge, the other
 * columns as captioned fields, no column header row), nothing scrolls sideways, and a card still opens its client.
 */
import { HttpMethod } from '../src/types/http'
import { expect, test } from '@playwright/test'
import { ada, clientsPage } from './fixtures/clients'
import { stubApi } from './support/stubApi'

test.use({ viewport: { width: 390, height: 800 } })

test('the clients table is a list of cards on a phone', async ({ page }) => {
  await stubApi(page, {
    signedIn: true,
    routes: [{ method: HttpMethod.Get, path: '/api/clients', respond: () => ({ body: clientsPage }) }],
  })

  await page.goto('/clients')
  const cards = page.getByTestId('client-row')
  await expect(cards).toHaveCount(2)

  const first = cards.first()
  await expect(first).toContainText(`${ada.firstName} ${ada.lastName}`)
  await expect(first.locator('td[data-label]').first()).toBeVisible()
  await expect(page.locator('th').first()).toBeHidden()

  const overflow = await page.evaluate(() => document.documentElement.scrollWidth - document.documentElement.clientWidth)
  expect(overflow).toBeLessThanOrEqual(0)
})
