/**
 * Flow: the client area's mobile navigation drawer.
 *
 * While the deployment is a demo the page carries a banner above the top bar. Opening the menu
 * must open exactly one drawer, below the top bar (never under it), listing every link the person
 * may see, and it must behave as a modal: Escape closes it, focus stays inside it and the page
 * behind it does not scroll. Checked in both themes at a phone and a tablet width.
 */
import type { Page } from '@playwright/test'
import { expect, test } from './support/test'
import { enableDemoMode, resetStub, signIn, waitForHydration } from './support/helpers'

/** Viewport widths the drawer is checked at (phone, tablet), in CSS pixels. */
const WIDTHS = [375, 768]

/** Colour schemes the drawer is checked in. */
const SCHEMES = ['light', 'dark'] as const

/** Every link a person with all permissions sees in the drawer. */
const NAV_IDS = ['nav-client-dashboard', 'nav-client-services', 'nav-client-domains', 'nav-client-invoices', 'nav-client-tickets', 'nav-announcements', 'nav-knowledgebase', 'nav-client-account']

/**
 * Opens the client dashboard with the demo banner on and the menu pressed open.
 *
 * @param page The page under test.
 * @returns Nothing; resolves once the drawer is on screen.
 */
const openMenu = async (page: Page): Promise<void> => {
  await page.goto('/client/dashboard')
  await waitForHydration(page)
  await page.getByTestId('client-menu-toggle').click()
  await expect(page.getByTestId('client-sidebar')).toHaveClass(/translate-x-0/)
}

for (const width of WIDTHS) {
  for (const scheme of SCHEMES) {
    test(`the drawer opens once, below the top bar, with every link (${width}px, ${scheme})`, async ({ page, context, request, baseURL }, testInfo) => {
      await resetStub(request)
      await enableDemoMode(request)
      await signIn(context, baseURL ?? 'http://127.0.0.1:3100')
      // The chosen colour mode lives in a cookie the server reads, so the first paint is already in it.
      await context.addCookies([{ name: 'color-mode', value: scheme, url: baseURL ?? 'http://127.0.0.1:3100' }])
      await page.setViewportSize({ width, height: 760 })

      await openMenu(page)

      if (scheme === 'dark') {
        await expect(page.locator('html')).toHaveClass(/\bdark\b/)
      } else {
        await expect(page.locator('html')).not.toHaveClass(/\bdark\b/)
      }
      const topBar = await page.getByTestId('client-topbar').boundingBox()
      const drawer = await page.getByTestId('client-sidebar').boundingBox()
      expect(topBar).not.toBeNull()
      expect(drawer).not.toBeNull()
      if (!topBar || !drawer) {
        return
      }
      // Below the banner and the top bar, so its first link is not clipped under either.
      expect(drawer.y).toBeGreaterThanOrEqual(topBar.y + topBar.height - 1)
      await expect(page.getByTestId('client-menu-toggle')).toBeVisible()
      for (const id of NAV_IDS) {
        await expect(page.getByTestId(id)).toBeVisible()
      }
      await page.screenshot({ path: testInfo.outputPath(`drawer-${width}-${scheme}.png`) })
    })
  }
}

test('Escape closes the drawer, focus is trapped while it is open and the page does not scroll', async ({ page, context, request, baseURL }) => {
  await resetStub(request)
  await signIn(context, baseURL ?? 'http://127.0.0.1:3100')
  await page.setViewportSize({ width: 375, height: 760 })

  await openMenu(page)
  await expect(page.locator('body')).toHaveClass(/overflow-hidden/)

  // Tab around more times than there are controls: focus never leaves the drawer.
  for (let i = 0; i < NAV_IDS.length + 3; i += 1) {
    await page.keyboard.press('Tab')
    const inside = await page.evaluate(() => Boolean(document.activeElement?.closest('[data-testid="client-sidebar"]')))
    expect(inside).toBe(true)
  }

  await page.keyboard.press('Escape')
  await expect(page.getByTestId('client-sidebar')).toHaveClass(/-translate-x-full/)
  await expect(page.locator('body')).not.toHaveClass(/overflow-hidden/)
  await expect(page.getByTestId('client-menu-toggle')).toBeFocused()
})

/** Viewport widths the header layout is checked at: a phone and a desktop. */
const HEADER_WIDTHS = [375, 1280]

for (const width of HEADER_WIDTHS) {
  test(`the logo is on the left of the top bar and the sidebar carries no product name (${width}px)`, async ({ page, context, request, baseURL }) => {
    await resetStub(request)
    await signIn(context, baseURL ?? 'http://127.0.0.1:3100')
    await page.setViewportSize({ width, height: 760 })
    await page.goto('/client/dashboard')
    await waitForHydration(page)

    // The brand sits at the left edge of the top bar, at every width.
    const brand = await page.getByTestId('client-topbar-brand').boundingBox()
    const topBar = await page.getByTestId('client-topbar').boundingBox()
    expect(brand).not.toBeNull()
    expect(topBar).not.toBeNull()
    expect(brand!.x - topBar!.x).toBeLessThan(40)

    // The drawer starts with the menu: there is no product name above it.
    await expect(page.getByTestId('client-sidebar-brand')).toHaveCount(0)

    // The welcome line is on the right of the top bar on a desktop, and hidden on a phone.
    const name = page.getByTestId('client-topbar-name')
    if (width >= 1024) {
      const box = await name.boundingBox()
      expect(box).not.toBeNull()
      expect(box!.x).toBeGreaterThan(brand!.x + brand!.width)
      expect(box!.x).toBeGreaterThan(topBar!.x + topBar!.width / 2)
    } else {
      await expect(name).toBeHidden()
    }
  })
}
