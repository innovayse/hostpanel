/**
 * The admin at phone width (390 px): every popup is one bottom sheet. The shell navigation opens from the top bar as a sheet
 * with a grip, closes on Escape, on a backdrop tap and on a drag down, and leads to a list; a dialog form is a sheet whose
 * save row is pinned to the bottom edge; on a list page the filters (titled Filters, with Apply and Clear in the footer
 * and the count on the trigger), a row menu, a select and the period picker open as sheets too. A report and an
 * integrations category are reached through a tab strip, a chart report fits, and a settings form keeps its save bar in
 * reach, with nothing scrolling sideways on any of them. At tablet width (768 px) the navigation is a sheet as well and five
 * key pages are checked for sideways scroll.
 */
import { HttpMethod } from '../src/types/http'
import { expect, test } from '@playwright/test'
import type { Page } from '@playwright/test'
import { clientsPage } from './fixtures/clients'
import { eur, usd } from './fixtures/currencies'
import { dashboardCharts, dashboardStats } from './fixtures/dashboard'
import { bankTransfer } from './fixtures/integrations'
import { annualIncome, topClients } from './fixtures/reports'
import { newTransaction, refundTransaction } from './fixtures/transactions'
import { boxOf, settingsPanel } from './support/locators'
import { json, stubApi } from './support/stubApi'
import { DashboardPeriod } from '../src/types/dashboardcharts'
import type { Setting } from '../src/types/setting'
import type { TransactionsResult } from '../src/types/transaction'

test.use({ viewport: { width: 390, height: 800 } })

/** A free-form setting the raw table of the Advanced tab edits. */
const timezone: Setting = { id: 90, key: 'general.timezone', value: 'UTC', description: 'Default time zone' }

/** The billing ledger the list-page sheets are checked on: two rows, with a row menu, filters, a select and a period picker. */
const ledger: TransactionsResult = {
  transactions: { items: [newTransaction, refundTransaction], totalCount: 2, pageNumber: 1, pageSize: 25, totalPages: 1 },
  totalIn: newTransaction.amountIn,
  totalOut: refundTransaction.amountOut,
  totalFees: 0,
  balance: newTransaction.amountIn - refundTransaction.amountOut,
  currency: 'USD',
}

/**
 * Reads how far any element of the page reaches past the viewport sideways. The shell scrolls inside its `<main>`
 * (which only clips sideways), so the document itself never overflows; the widest element is what shows a screen that is
 * too wide. An element inside a box that scrolls sideways on purpose (a table in its card) is cut at that box's edge,
 * because the box, not the page, holds the extra width; a box that merely hides overflow does not excuse anything.
 *
 * @param page - The page under test.
 * @returns The overflow in pixels; zero or less means nothing reaches past the viewport.
 */
const sidewaysOverflow = (page: Page): Promise<number> =>
  page.evaluate(() => {
    let widest = 0
    for (const element of document.body.querySelectorAll('*')) {
      let right = element.getBoundingClientRect().right
      for (let box = element.parentElement; box !== null; box = box.parentElement) {
        const overflowX = getComputedStyle(box).overflowX
        if (overflowX === 'auto' || overflowX === 'scroll') {
          right = Math.min(right, box.getBoundingClientRect().right)
        }
      }
      widest = Math.max(widest, right)
    }
    return Math.ceil(widest - document.documentElement.clientWidth)
  })

test('the menu sheet opens from the top bar with a grip, leads to a list and closes', async ({ page }) => {
  await stubApi(page, { signedIn: true, routes: [json(HttpMethod.Get, '/api/clients', clientsPage)] })

  await page.goto('/')
  await expect(page.getByTestId('sidebar-desktop')).toHaveCount(1)
  await expect(page.getByTestId('sidebar-desktop')).toBeHidden()

  await page.getByTestId('topbar-toggle-sidebar').click()
  await expect(page.getByTestId('drawer')).toBeVisible()
  await expect(page.getByTestId('drawer-grip')).toBeVisible()
  // Clients is a section: its button opens the sub-menu, whose first link (View) shares the entry's test id and leads to the list.
  await page.getByTestId('drawer').getByTestId('nav-clients').first().click()
  await page.getByTestId('drawer').getByTestId('nav-clients').last().click()

  await expect(page).toHaveURL(/\/clients$/)
  await expect(page.getByTestId('drawer')).toHaveCount(0)
  await expect(page.getByTestId('client-row')).toHaveCount(clientsPage.items.length)
  expect(await sidewaysOverflow(page)).toBeLessThanOrEqual(0)
})

test('the menu sheet closes on Escape without leading anywhere', async ({ page }) => {
  await stubApi(page, { signedIn: true, routes: [json(HttpMethod.Get, '/api/clients', clientsPage)] })

  await page.goto('/')
  await page.getByTestId('topbar-toggle-sidebar').click()
  await expect(page.getByTestId('drawer-grip')).toBeVisible()
  await expect(page.getByTestId('drawer-close')).toBeVisible()

  await page.keyboard.press('Escape')

  await expect(page.getByTestId('drawer')).toHaveCount(0)
  await expect(page).toHaveURL(/\/dashboard$/)
  expect(await sidewaysOverflow(page)).toBeLessThanOrEqual(0)
})

test('the menu sheet closes when its grip is dragged down', async ({ page }) => {
  await stubApi(page, { signedIn: true, routes: [json(HttpMethod.Get, '/api/clients', clientsPage)] })

  await page.goto('/')
  await page.getByTestId('topbar-toggle-sidebar').click()
  await expect(page.getByTestId('drawer-close')).toBeVisible()
  const grip = await boxOf(page.getByTestId('drawer-grip'))
  const x = grip.x + grip.width / 2
  const y = grip.y + grip.height / 2

  await page.mouse.move(x, y)
  await page.mouse.down()
  await page.mouse.move(x, y + 320, { steps: 8 })
  await page.mouse.up()

  await expect(page.getByTestId('drawer')).toHaveCount(0)
  await expect(page).toHaveURL(/\/dashboard$/)
})

test('a form dialog is a bottom sheet with its save row pinned to the bottom edge', async ({ page }) => {
  await stubApi(page, { signedIn: true, routes: [json(HttpMethod.Get, '/api/admin/currencies', [usd, eur])] })

  await page.goto('/settings/currencies')
  await page.getByTestId(`currency-actions-${eur.code}`).click()
  await page.getByTestId(`currency-edit-${eur.code}`).click()

  const panel = page.getByTestId('dialog-panel')
  await expect(panel).toBeVisible()
  // The row menu sheet that led here is still leaving while the dialog rises, so the grip is awaited until it is the dialog's alone.
  await expect(page.getByTestId('drawer-grip')).toHaveCount(1)
  // The sheet rises into place, so the box is read until the enter has settled: full width, resting on the bottom edge, under the height limit.
  await expect.poll(async () => (await panel.boundingBox())?.width).toBe(390)
  await expect.poll(async () => {
    const box = await panel.boundingBox()
    return box === null ? 0 : box.y + box.height
  }).toBe(800)
  expect((await boxOf(panel)).height).toBeLessThanOrEqual(800 * 0.85 + 1)

  // The save button is reachable without scrolling the dialog and sits inside the viewport.
  const save = page.getByTestId('currency-form-save')
  await expect(save).toBeInViewport()
  // The action row sticks to the bottom edge: it sits in the last strip of the screen, not wherever the form ends (the buttons stack, the save on top of the cancel).
  const actions = page.getByTestId('ui-form-actions')
  await expect.poll(async () => {
    const box = await actions.boundingBox()
    return box === null ? 0 : 800 - (box.y + box.height)
  }).toBeLessThanOrEqual(32)
  expect(await sidewaysOverflow(page)).toBeLessThanOrEqual(0)
})

test.describe('the popups of a list page are bottom sheets', () => {
  /**
   * Stubs the billing ledger and opens it.
   *
   * @param page - The page under test.
   * @returns Nothing; the ledger is on screen.
   */
  const openLedger = async (page: Page): Promise<void> => {
    await stubApi(page, {
      signedIn: true,
      routes: [json(HttpMethod.Get, '/api/transactions', ledger), json(HttpMethod.Get, '/api/clients', clientsPage)],
    })
    await page.goto('/billing/transactions')
    await expect(page.getByTestId('billing-transactions-table')).toContainText(newTransaction.description)
  }

  test('the filters are a sheet titled Filters with Apply and Clear in the footer, and the count stays on the trigger', async ({ page }) => {
    await openLedger(page)

    await page.getByTestId('billing-transactions-filter').click()
    await expect(page.getByTestId('drawer-grip')).toBeVisible()
    await expect(page.getByRole('heading', { name: 'Filters' })).toBeVisible()
    await expect(page.getByTestId('ui-drawer-footer').getByTestId('filter-apply')).toBeVisible()
    await expect(page.getByTestId('ui-drawer-footer').getByTestId('filter-reset')).toHaveText('Clear')
    await expect(page.getByTestId('filter-cancel')).toBeHidden()

    await page.getByTestId('billing-transactions-filter-direction').getByTestId('ui-select-trigger').click()
    // The first option is "All activity", which filters nothing; the second narrows the list and is what the trigger counts.
    await page.getByTestId('ui-select-option').nth(1).click()
    await page.getByTestId('filter-apply').click()

    await expect(page.getByTestId('dialog-panel')).toHaveCount(0)
    await expect(page.getByTestId('filter-trigger-count')).toHaveText('1')
  })

  test('a row menu opens as a sheet and closes on the backdrop', async ({ page }) => {
    await openLedger(page)

    await page.getByTestId(`billing-transaction-actions-${newTransaction.id}`).click()
    await expect(page.getByTestId('drawer-grip')).toBeVisible()
    await expect(page.getByTestId('row-actions-menu')).toBeVisible()
    await expect(page.getByTestId(`billing-transaction-edit-${newTransaction.id}`)).toBeVisible()

    await page.mouse.click(195, 20)

    await expect(page.getByTestId('drawer')).toHaveCount(0)
  })

  test('a select lists its options in a sheet and picking one closes it', async ({ page }) => {
    await openLedger(page)
    await page.getByTestId('billing-transactions-filter').click()
    await expect(page.getByTestId('dialog-panel')).toBeVisible()

    await page.getByTestId('billing-transactions-filter-direction').getByTestId('ui-select-trigger').click()

    // The select's own sheet (the drawer) sits above the filters sheet (the dialog panel).
    await expect(page.getByTestId('drawer-grip')).toHaveCount(2)
    await expect(page.getByTestId('ui-select-panel')).toBeVisible()
    await page.getByTestId('ui-select-option').first().click()
    await expect(page.getByTestId('ui-select-panel')).toHaveCount(0)
    await expect(page.getByTestId('drawer')).toHaveCount(0)
    await expect(page.getByTestId('dialog-panel')).toBeVisible()
  })

  test('the period picker opens as a sheet from a tap on its field', async ({ page }) => {
    await openLedger(page)
    await page.getByTestId('billing-transactions-filter').click()

    await page.getByTestId('date-range-input').click()

    await expect(page.getByTestId('drawer-grip')).toHaveCount(2)
    await expect(page.getByTestId('date-range-apply')).toBeVisible()
    await page.getByTestId('date-range-preset-today').click()
    await expect(page.getByTestId('date-range-apply')).toHaveCount(0)
    await expect(page.getByTestId('drawer')).toHaveCount(0)
    await expect(page.getByTestId('dialog-panel')).toBeVisible()
  })
})

test('a report is chosen from the tab strip and fits the phone', async ({ page }) => {
  await stubApi(page, { signedIn: true, routes: [json(HttpMethod.Get, '/api/reports/top-clients-by-income', topClients)] })

  await page.goto('/reports/top-clients')

  await expect(page.getByTestId('reports-tabs')).toBeVisible()
  // The side navigation stays in the page, hidden, behind the strip; its search box exists only while that sidebar is open.
  await expect(page.getByTestId('reports-nav-top-clients')).toHaveCount(1)
  await expect(page.getByTestId('reports-nav-top-clients')).toBeHidden()
  await expect(page.getByTestId('reports-nav-search')).toHaveCount(0)
  await expect(page.getByTestId('report-table-row')).toHaveCount(topClients.length)
  expect(await sidewaysOverflow(page)).toBeLessThanOrEqual(0)
})

test('an integrations category is chosen from the tab strip and fits the phone', async ({ page }) => {
  await stubApi(page, { signedIn: true, routes: [json(HttpMethod.Get, '/api/admin/integrations', [bankTransfer])] })

  await page.goto('/integrations/payments')

  await expect(page.getByTestId('integrations-tabs')).toBeVisible()
  await expect(page.getByTestId('integrations-nav-payments')).toHaveCount(1)
  await expect(page.getByTestId('integrations-nav-payments')).toBeHidden()
  await expect(page.getByTestId(`integration-card-${bankTransfer.slug}`)).toBeVisible()
  expect(await sidewaysOverflow(page)).toBeLessThanOrEqual(0)
})

test('a chart report draws its chart inside the phone', async ({ page }) => {
  await stubApi(page, { signedIn: true, routes: [json(HttpMethod.Get, '/api/reports/annual-income', annualIncome)] })

  await page.goto('/reports/annual-income')

  await expect(page.getByTestId('report-chart')).toBeVisible()
  expect(await sidewaysOverflow(page)).toBeLessThanOrEqual(0)
})

test('a settings form keeps its save bar in reach and fits the phone', async ({ page }) => {
  await stubApi(page, { signedIn: true, routes: [json(HttpMethod.Get, '/api/admin/settings', [timezone])] })

  await page.goto('/settings/general?tab=advanced')

  await expect(settingsPanel(page, 'advanced').getByTestId('settings-form-save')).toBeInViewport()
  expect(await sidewaysOverflow(page)).toBeLessThanOrEqual(0)
})

test.describe('at tablet width', () => {
  test.use({ viewport: { width: 768, height: 1024 } })

  /** Five key pages, each with the stub routes it needs and the test id that shows its content has arrived. */
  const pages = [
    {
      name: 'the dashboard',
      path: '/dashboard',
      routes: [json(HttpMethod.Get, '/api/admin/dashboard/stats', dashboardStats), json(HttpMethod.Get, '/api/admin/dashboard/charts', dashboardCharts[DashboardPeriod.Days30])],
      ready: 'dashboard-stats',
    },
    { name: 'the client list', path: '/clients', routes: [json(HttpMethod.Get, '/api/clients', clientsPage)], ready: 'client-row' },
    { name: 'a chart report', path: '/reports/annual-income', routes: [json(HttpMethod.Get, '/api/reports/annual-income', annualIncome)], ready: 'report-chart' },
    { name: 'the currencies list', path: '/settings/currencies', routes: [json(HttpMethod.Get, '/api/admin/currencies', [usd, eur])], ready: `currency-actions-${eur.code}` },
    { name: 'the system settings', path: '/settings/general?tab=advanced', routes: [json(HttpMethod.Get, '/api/admin/settings', [timezone])], ready: `setting-value-${timezone.id}` },
  ]

  test('the navigation opens as a bottom sheet on the icon rail', async ({ page }) => {
    await stubApi(page, { signedIn: true, routes: [json(HttpMethod.Get, '/api/clients', clientsPage)] })

    await page.goto('/')
    await page.getByTestId('topbar-toggle-sidebar').click()

    await expect(page.getByTestId('drawer-grip')).toBeVisible()
    await expect(page.getByTestId('drawer').getByTestId('nav-clients').last()).toBeVisible()
    await page.keyboard.press('Escape')
    await expect(page.getByTestId('drawer')).toHaveCount(0)
  })

  for (const entry of pages) {
    test(`${entry.name} does not scroll sideways`, async ({ page }) => {
      await stubApi(page, { signedIn: true, routes: entry.routes })

      await page.goto(entry.path)
      await expect(page.getByTestId(entry.ready).first()).toBeVisible()

      expect(await sidewaysOverflow(page)).toBeLessThanOrEqual(0)
    })
  }
})
