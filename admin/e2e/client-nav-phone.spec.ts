/**
 * Client pages at phone width: the section navigation is a horizontal tab strip above the content
 * instead of a vertical icon rail, the client header and the cards use the full width, nothing
 * scrolls sideways, a tab opens its section, and the top bar fits.
 */
import { HttpMethod } from '../src/types/http'
import { expect, test } from '@playwright/test'
import { adaDetail } from './fixtures/clients'
import { emptyClientSummary } from './fixtures/clientSummary'
import { json, stubApi } from './support/stubApi'

/** An empty page of a paged list. */
const emptyPage = { items: [], pageNumber: 1, pageSize: 5, totalCount: 0, totalPages: 0 }

test.use({ viewport: { width: 390, height: 800 } })

test('the client sections are a tab strip above the content on a phone', async ({ page }) => {
  await stubApi(page, {
    signedIn: true,
    routes: [
      json(HttpMethod.Get, '/api/clients/7', adaDetail),
      json(HttpMethod.Get, '/api/clients/7/summary', emptyClientSummary),
      json(HttpMethod.Get, '/api/services', emptyPage),
      json(HttpMethod.Get, '/api/domains', emptyPage),
      json(HttpMethod.Get, '/api/billing/quotes/client/7', emptyPage),
    ],
  })

  await page.goto('/clients/7/summary')

  const strip = page.getByTestId('client-nav-strip')
  await expect(strip).toBeVisible()
  await expect(page.getByTestId('client-nav-rail')).toBeHidden()

  // The strip sits above the content and scrolls on its own; the page itself does not.
  const stripBox = await strip.boundingBox()
  const cardBox = await page.getByTestId('client-summary-info').boundingBox()
  expect(stripBox!.y + stripBox!.height).toBeLessThanOrEqual(cardBox!.y)
  expect(await page.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth)).toBe(true)

  // The header and the first card use the width of the phone, and are not cut off.
  const header = await page.getByTestId('client-layout-header').boundingBox()
  expect(header!.x).toBeGreaterThanOrEqual(0)
  expect(header!.x + header!.width).toBeLessThanOrEqual(390)
  expect(cardBox!.x + cardBox!.width).toBeLessThanOrEqual(390)

  await expect(page.getByTestId('client-tab-summary')).toHaveAttribute('aria-selected', 'true')
})

test('the client header on a phone: the status sits beside the name and Export is a row of its own', async ({ page }) => {
  await stubApi(page, {
    signedIn: true,
    routes: [
      json(HttpMethod.Get, '/api/clients/7', { ...adaDetail, firstName: 'Ada', lastName: 'Li' }),
      json(HttpMethod.Get, '/api/clients/7/summary', emptyClientSummary),
      json(HttpMethod.Get, '/api/services', emptyPage),
      json(HttpMethod.Get, '/api/domains', emptyPage),
      json(HttpMethod.Get, '/api/billing/quotes/client/7', emptyPage),
    ],
  })

  await page.goto('/clients/7/summary')

  const back = await page.getByTestId('client-layout-back').boundingBox()
  const name = await page.getByTestId('client-header-name').boundingBox()
  const status = await page.getByTestId('client-layout-header').getByText(adaDetail.status, { exact: true }).boundingBox()
  const exportButton = await page.getByTestId('client-layout-export').boundingBox()

  // First row: the back arrow, the name and the status badge right after it share a line.
  expect(Math.abs((back!.y + back!.height / 2) - (name!.y + name!.height / 2))).toBeLessThan(20)
  expect(Math.abs((status!.y + status!.height / 2) - (name!.y + name!.height / 2))).toBeLessThan(20)
  expect(status!.x).toBeGreaterThanOrEqual(name!.x + name!.width - 4)
  // Second row: Export is below the name and stays on screen.
  expect(exportButton!.y).toBeGreaterThan(name!.y + name!.height - 4)
  expect(exportButton!.x + exportButton!.width).toBeLessThanOrEqual(390)
})

test('the top bar fits a phone: the sign-out button is on screen and the controls that do not fit are left out', async ({ page }) => {
  await stubApi(page, {
    signedIn: true,
    routes: [
      json(HttpMethod.Get, '/api/clients/7', adaDetail),
      json(HttpMethod.Get, '/api/clients/7/summary', emptyClientSummary),
      json(HttpMethod.Get, '/api/services', emptyPage),
      json(HttpMethod.Get, '/api/domains', emptyPage),
      json(HttpMethod.Get, '/api/billing/quotes/client/7', emptyPage),
    ],
  })

  await page.goto('/clients/7/summary')

  const signOut = await page.getByTestId('topbar-sign-out').boundingBox()
  expect(signOut!.x + signOut!.width).toBeLessThanOrEqual(390)
  await expect(page.getByTestId('topbar-toggle-sidebar')).toBeVisible()
  await expect(page.getByTestId('topbar-notifications')).toBeVisible()
  await expect(page.getByTestId('topbar-theme')).toBeHidden()
  await expect(page.getByTestId('topbar-settings')).toBeHidden()
})

test('a tab on the phone strip opens its section', async ({ page }) => {
  await stubApi(page, {
    signedIn: true,
    routes: [
      json(HttpMethod.Get, '/api/clients/7', adaDetail),
      json(HttpMethod.Get, '/api/clients/7/summary', emptyClientSummary),
      json(HttpMethod.Get, '/api/services', emptyPage),
      json(HttpMethod.Get, '/api/domains', emptyPage),
      json(HttpMethod.Get, '/api/billing/quotes/client/7', emptyPage),
      json(HttpMethod.Get, '/api/billing/client/7', emptyPage),
    ],
  })

  await page.goto('/clients/7/summary')
  await page.getByTestId('client-tab-invoices').click()

  await page.waitForURL('**/clients/7/invoices')
  await expect(page.getByTestId('client-tab-invoices')).toHaveAttribute('aria-selected', 'true')
})

test.describe('on a tablet and wider', () => {
  test.use({ viewport: { width: 1024, height: 800 } })

  test('the icon rail is back and the strip is gone', async ({ page }) => {
    await stubApi(page, {
      signedIn: true,
      routes: [
        json(HttpMethod.Get, '/api/clients/7', adaDetail),
        json(HttpMethod.Get, '/api/clients/7/summary', emptyClientSummary),
        json(HttpMethod.Get, '/api/services', emptyPage),
        json(HttpMethod.Get, '/api/domains', emptyPage),
        json(HttpMethod.Get, '/api/billing/quotes/client/7', emptyPage),
      ],
    })

    await page.goto('/clients/7/summary')

    await expect(page.getByTestId('client-nav-rail')).toBeVisible()
    await expect(page.getByTestId('client-nav-strip')).toBeHidden()
    await expect(page.getByTestId('topbar-theme')).toBeVisible()
    await expect(page.getByTestId('topbar-settings')).toBeVisible()
  })
})

test('the billing card lines its counts and amounts up in columns, and the summary tables scroll inside their cards', async ({ page }) => {
  await stubApi(page, {
    signedIn: true,
    routes: [
      json(HttpMethod.Get, '/api/clients/7', adaDetail),
      json(HttpMethod.Get, '/api/clients/7/summary', emptyClientSummary),
      json(HttpMethod.Get, '/api/services', emptyPage),
      json(HttpMethod.Get, '/api/domains', emptyPage),
      json(HttpMethod.Get, '/api/billing/quotes/client/7', emptyPage),
    ],
  })

  await page.goto('/clients/7/summary')
  await expect(page.getByTestId('client-summary-billing')).toBeVisible()

  // Every amount ends on the same line, whatever its width, and so does every count.
  const edges = async (selector: string): Promise<number[]> =>
    page.locator(selector).evaluateAll((els) => els.map((el) => Math.round(el.getBoundingClientRect().right)))
  const amounts = await edges('[data-testid="client-summary-billing"] .tabular-nums.min-w-\\[5\\.5rem\\]')
  expect(amounts.length).toBeGreaterThan(5)
  expect(new Set(amounts).size).toBe(1)
  const counts = await edges('[data-testid="client-summary-billing"] .tabular-nums.w-6')
  expect(new Set(counts).size).toBe(1)

  // The wide tables do not widen the page: the card scrolls, the page does not.
  expect(await page.evaluate(() => {
    const main = document.querySelector('main')!
    return main.scrollWidth <= main.clientWidth && document.documentElement.scrollWidth <= window.innerWidth
  })).toBe(true)
  const card = page.getByTestId('client-summary-services').or(page.locator('main table').first().locator('xpath=ancestor::div[contains(@class,"overflow-x-auto")][1]'))
  await expect(card.first()).toBeAttached()
})

