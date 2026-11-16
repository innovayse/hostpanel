/**
 * Clients list at phone width: the table is drawn as cards (name as the heading, status as the badge, the other
 * columns as captioned fields, no column header row), nothing scrolls sideways, and a card still opens its client.
 * The filters open as a bottom sheet, the applied filters are counted on the button, kept in the address across a
 * reload, and cleared with Reset and Apply.
 */
import { HttpMethod } from '../src/types/http'
import { expect, test } from '@playwright/test'
import { ada, clientsPage } from './fixtures/clients'
import { fieldOf } from './support/locators'
import { json, stubApi } from './support/stubApi'

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
  // The email column is hidden below lg in the table, so its header asks to stay on the card, captioned by real text.
  await expect(first.getByText('Email', { exact: true })).toBeVisible()
  await expect(first).toContainText(ada.email)
  // The id column is hidden below md and stays off the card.
  await expect(first.getByText('ID', { exact: true })).toHaveCount(0)
  await expect(page.locator('th').first()).toBeHidden()

  const overflow = await page.evaluate(() => document.documentElement.scrollWidth - document.documentElement.clientWidth)
  expect(overflow).toBeLessThanOrEqual(0)
})

test('the filters open as a bottom sheet, are counted, survive a reload and are reset', async ({ page }) => {
  const api = await stubApi(page, {
    signedIn: true,
    routes: [json(HttpMethod.Get, '/api/clients', clientsPage)],
  })

  await page.goto('/clients')
  await expect(page.getByTestId('client-row')).toHaveCount(2)

  // On a phone the filter surface is the sheet: it carries the grip a dialog does not.
  await page.getByTestId('filter-trigger').click()
  await expect(page.getByTestId('drawer-grip')).toBeVisible()
  await fieldOf(page, 'clients-list-filter-email').fill('grace')
  await page.getByTestId('filter-apply').click()

  await expect(page).toHaveURL(/email=grace/)
  await expect(page.getByTestId('filter-trigger-count')).toHaveText('1')
  expect(api.callsTo(HttpMethod.Get, '/api/clients').at(-1)?.search).toContain('email=grace')

  // The address holds the filter, so a reload shows the same chip and asks the API with it again.
  await page.reload()
  await expect(page.getByTestId('filter-chip-email')).toContainText('grace')
  expect(api.callsTo(HttpMethod.Get, '/api/clients').at(-1)?.search).toContain('email=grace')

  await page.getByTestId('filter-trigger').click()
  await page.getByTestId('filter-reset').click()
  await page.getByTestId('filter-apply').click()

  await expect(page).not.toHaveURL(/email=/)
  await expect(page.getByTestId('filter-trigger-count')).toHaveCount(0)
  expect(api.callsTo(HttpMethod.Get, '/api/clients').at(-1)?.search).not.toContain('email=')
})
