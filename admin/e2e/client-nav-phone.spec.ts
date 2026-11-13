/**
 * Client pages at phone width: the section navigation is a horizontal tab strip above the content
 * (the same strip at every width), the client header card and the cards use the full width, nothing
 * scrolls sideways, a tab opens its section, the top bar fits, and the theme is changed from the menu drawer.
 */
import { HttpMethod } from '../src/types/http'
import { AdminTheme } from '../src/types/admintheme'
import { THEME_STORAGE_KEY } from '../src/lib/themeBoot'
import { expect, test } from '@playwright/test'
import type { Locator } from '@playwright/test'
import { adaDetail } from './fixtures/clients'
import { servicesPage } from './fixtures/orders'
import { emptyClientSummary } from './fixtures/clientSummary'
import { json, stubApi } from './support/stubApi'

/** An empty page of a paged list. */
const emptyPage = { items: [], pageNumber: 1, pageSize: 5, totalCount: 0, totalPages: 0 }

test.use({ viewport: { width: 390, height: 800 } })

/** Most a box can sit past its scroller's edge because the browser rounds a scroll offset to whole pixels. */
const SCROLL_ROUNDING_PX = 1

/**
 * Measures an element that must be on screen.
 *
 * @param locator - The element.
 * @returns Its bounding box.
 * @throws When the element is not rendered, so a missing element fails the test with its name instead of a null read.
 */
const boxOf = async (locator: Locator): Promise<{ x: number, y: number, width: number, height: number }> => {
  const box = await locator.boundingBox()
  if (box === null) {
    throw new Error(`${String(locator)} has no box: it is not rendered`)
  }
  return box
}

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

  // The strip sits above the content and scrolls on its own; the page itself does not.
  const stripBox = await boxOf(strip)
  const cardBox = await boxOf(page.getByTestId('client-summary-info'))
  expect(stripBox.y + stripBox.height).toBeLessThanOrEqual(cardBox.y)
  expect(await page.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth)).toBe(true)

  // The header and the first card use the width of the phone, and are not cut off.
  const header = await boxOf(page.getByTestId('client-layout-header'))
  expect(header.x).toBeGreaterThanOrEqual(0)
  expect(header.x + header.width).toBeLessThanOrEqual(390)
  expect(cardBox.x + cardBox.width).toBeLessThanOrEqual(390)

  await expect(page.getByTestId('client-tab-summary')).toHaveAttribute('aria-selected', 'true')

  // The 13 tabs are wider than a phone, so the strip itself scrolls sideways.
  const tablist = strip.getByRole('tablist')
  const sizes = await tablist.evaluate(node => ({ scrollWidth: node.scrollWidth, clientWidth: node.clientWidth }))
  expect(sizes.scrollWidth).toBeGreaterThan(sizes.clientWidth)
})

test('a tab far along the strip is brought into view when its section opens', async ({ page }) => {
  await stubApi(page, {
    signedIn: true,
    routes: [
      json(HttpMethod.Get, '/api/clients/7', adaDetail),
      json(HttpMethod.Get, '/api/clients/7/summary', emptyClientSummary),
    ],
  })

  await page.goto('/clients/7/log')

  const logTab = page.getByTestId('client-tab-log')
  await expect(logTab).toHaveAttribute('aria-selected', 'true')
  // The strip scrolls a frame after the tab is marked selected, and again when it changes size as the page
  // below it loads, so both boxes are read on every poll rather than once. A scroll offset is a whole number
  // of pixels while the tabs have fractional widths, so the last tab, scrolled as far as it goes, can end up
  // to one pixel past the strip's edge; less than that is in view.
  await expect.poll(async () => {
    const stripBox = await boxOf(page.getByTestId('client-nav-strip').getByRole('tablist'))
    const tabBox = await boxOf(logTab)
    return tabBox.x >= stripBox.x - SCROLL_ROUNDING_PX
      && tabBox.x + tabBox.width <= stripBox.x + stripBox.width + SCROLL_ROUNDING_PX
  }).toBe(true)
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

  const back = await boxOf(page.getByTestId('client-layout-back'))
  const name = await boxOf(page.getByTestId('client-header-name'))
  const status = await boxOf(page.getByTestId('client-layout-header').getByText(adaDetail.status, { exact: true }))
  const exportButton = await boxOf(page.getByTestId('client-layout-export'))

  // First row: the back arrow, the name and the status badge right after it share a line.
  expect(Math.abs((back.y + back.height / 2) - (name.y + name.height / 2))).toBeLessThan(20)
  expect(Math.abs((status.y + status.height / 2) - (name.y + name.height / 2))).toBeLessThan(20)
  expect(status.x).toBeGreaterThanOrEqual(name.x + name.width - 4)
  // Second row: Export is below the name and stays on screen.
  expect(exportButton.y).toBeGreaterThan(name.y + name.height - 4)
  expect(exportButton.x + exportButton.width).toBeLessThanOrEqual(390)
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

  const bell = await boxOf(page.getByTestId('topbar-notifications'))
  expect(bell.x + bell.width).toBeLessThanOrEqual(390)
  await expect(page.getByTestId('topbar-toggle-sidebar')).toBeVisible()
  // The page still has a heading on a phone, and it stays left of the controls.
  const title = await boxOf(page.getByTestId('topbar-title'))
  expect(title.x + title.width).toBeLessThanOrEqual(bell.x)
  await expect(page.getByTestId('topbar-notifications')).toBeVisible()
  await expect(page.getByTestId('topbar-theme')).toBeHidden()
})

test('a phone changes the theme from the menu drawer, since the top bar has no room for the picker', async ({ page }) => {
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
  await page.emulateMedia({ colorScheme: 'dark' })

  await page.goto('/clients/7/summary')
  await expect(page.getByTestId('topbar-theme')).toBeHidden()

  await page.getByTestId('topbar-toggle-sidebar').click()
  await expect(page.getByTestId('sidebar-theme')).toBeVisible()
  await page.getByTestId('sidebar-theme').click()
  await page.getByTestId('theme-option-light').click()

  await expect(page.locator('html')).toHaveClass(/(^|\s)light(\s|$)/)
  expect(await page.evaluate(key => window.localStorage.getItem(key), THEME_STORAGE_KEY)).toBe(AdminTheme.Light)
})

test('the contacts table on the profile page scrolls inside its card and does not widen the page', async ({ page }) => {
  const contact = {
    firstName: 'Anna', lastName: 'Petrosyan', email: 'anna.petrosyan.billing@example.test', phone: '+37410111222',
    address: { street: null, address2: null, city: null, state: null, postCode: null, country: null },
    notifications: { general: true, invoice: true, support: false, product: false, domain: false, affiliate: false },
    createdAt: '2026-01-15T10:00:00Z',
  }
  await stubApi(page, {
    signedIn: true,
    routes: [
      json(HttpMethod.Get, '/api/clients/7', { ...adaDetail, contacts: [{ ...contact, id: 1, type: 'Billing' }, { ...contact, id: 2, type: 'Technical' }] }),
    ],
  })

  await page.goto('/clients/7/profile')
  const table = page.getByTestId('client-profile-contacts')
  await expect(table).toBeVisible()

  const scrolling = await table.evaluate((el) => {
    let node: HTMLElement | null = el.parentElement
    while (node !== null && !['auto', 'scroll'].includes(getComputedStyle(node).overflowX)) {
      node = node.parentElement
    }
    return node === null ? null : { scrollWidth: node.scrollWidth, clientWidth: node.clientWidth, isContentScroller: node.classList.contains('overflow-y-auto') }
  })
  // The scroller must be the card, not the content column itself (which scrolls sideways, cutting the page, when the card does not).
  expect(scrolling?.isContentScroller).toBe(false)
  expect(scrolling?.scrollWidth).toBeGreaterThan(scrolling?.clientWidth ?? Infinity)
  expect(await page.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth)).toBe(true)
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

  test('the same tab strip is shown, there is no icon rail, and the top bar has its full controls', async ({ page }) => {
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

    await expect(page.getByTestId('client-nav-strip')).toBeVisible()
    await expect(page.getByTestId('client-tab-summary')).toHaveAttribute('aria-selected', 'true')
    await expect(page.getByTestId('topbar-theme')).toBeVisible()
    await page.getByTestId('sidebar-user-menu').click()
    await expect(page.getByTestId('sidebar-settings')).toBeVisible()
  })
})

test('the billing card lines its counts and amounts up in columns, and the summary tables scroll inside their cards', async ({ page }) => {
  await stubApi(page, {
    signedIn: true,
    routes: [
      json(HttpMethod.Get, '/api/clients/7', adaDetail),
      json(HttpMethod.Get, '/api/clients/7/summary', emptyClientSummary),
      json(HttpMethod.Get, '/api/services', servicesPage),
      json(HttpMethod.Get, '/api/domains', emptyPage),
      json(HttpMethod.Get, '/api/billing/quotes/client/7', emptyPage),
    ],
  })

  await page.goto('/clients/7/summary')
  await expect(page.getByTestId('client-summary-billing')).toBeVisible()

  // Every amount ends on the same line, whatever its width, and so does every count.
  const edges = async (testid: string): Promise<number[]> =>
    page.getByTestId(testid).evaluateAll((els) => els.map((el) => Math.round(el.getBoundingClientRect().right)))
  const amounts = await edges('client-summary-billing-amount')
  expect(amounts.length).toBeGreaterThan(5)
  expect(new Set(amounts).size).toBe(1)
  const counts = await edges('client-summary-billing-count')
  expect(counts.length).toBeGreaterThan(5)
  expect(new Set(counts).size).toBe(1)

  // The wide tables do not widen the page: the card scrolls, the page does not.
  expect(await page.evaluate(() => {
    const main = document.querySelector('main')
    if (main === null) {
      throw new Error('the page has no <main>')
    }
    return main.scrollWidth <= main.clientWidth && document.documentElement.scrollWidth <= window.innerWidth
  })).toBe(true)
  // The services table is wider than a phone: its card scrolls sideways inside itself, which is what keeps the page still.
  await expect(page.getByTestId('client-summary-services')).toBeVisible()
  const scrolling = await page.getByTestId('client-summary-services').evaluate((table) => {
    let node: HTMLElement | null = table.parentElement
    while (node !== null && !['auto', 'scroll'].includes(getComputedStyle(node).overflowX)) {
      node = node.parentElement
    }
    return node === null ? null : { scrollWidth: node.scrollWidth, clientWidth: node.clientWidth, isContentScroller: node.classList.contains('overflow-y-auto') }
  })
  expect(scrolling).not.toBeNull()
  // The scroller must be the card, not the content column itself (which scrolls sideways, cutting the page, when the card does not).
  expect(scrolling?.isContentScroller).toBe(false)
  expect(scrolling?.scrollWidth).toBeGreaterThan(scrolling?.clientWidth ?? Infinity)
})
