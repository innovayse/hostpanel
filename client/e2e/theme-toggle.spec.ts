/**
 * Flow: light/dark toggle.
 *
 * The visitor switches the colour mode; the page swaps to the dark palette and the header swaps
 * the light wordmark for the dark one, and the choice survives a reload (it lives in a cookie so
 * the server renders the right class on first paint). The stub serves an uploaded light/dark
 * logo pair and a light default, so the starting point is known.
 */
import { expect, test } from './support/test'
import { waitForHydration } from './support/helpers'

test('the toggle switches the palette and the logo, and the choice persists across a reload', async ({ page }) => {
  await page.goto('/')
  await waitForHydration(page)

  // Light by operator default: the light wordmark shows, the dark one is hidden.
  await expect(page.locator('html')).not.toHaveClass(/\bdark\b/)
  await expect(page.getByTestId('app-logo-light')).toBeVisible()
  await expect(page.getByTestId('app-logo-dark')).toBeHidden()

  // Toggle -> dark palette, and the wordmarks swap.
  await page.getByTestId('theme-toggle').click()
  await expect(page.locator('html')).toHaveClass(/\bdark\b/)
  await expect(page.getByTestId('app-logo-dark')).toBeVisible()
  await expect(page.getByTestId('app-logo-light')).toBeHidden()

  // The choice is remembered: after a reload the server renders dark again.
  await page.reload()
  await waitForHydration(page)
  await expect(page.locator('html')).toHaveClass(/\bdark\b/)
  await expect(page.getByTestId('app-logo-dark')).toBeVisible()
  await expect(page.getByTestId('app-logo-light')).toBeHidden()

  // And back to light.
  await page.getByTestId('theme-toggle').click()
  await expect(page.locator('html')).not.toHaveClass(/\bdark\b/)
  await expect(page.getByTestId('app-logo-light')).toBeVisible()
})
