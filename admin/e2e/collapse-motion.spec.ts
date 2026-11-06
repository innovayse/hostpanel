/**
 * Collapsible blocks: a block that opens or closes keeps its height animating (it is still in the page just after
 * the click), then is gone, and under reduced motion it is gone at once. Run on the client log filters.
 */
import { expect, test } from '@playwright/test'
import type { Page } from '@playwright/test'
import { adaDetail } from './fixtures/clients'
import { emptyClientSummary } from './fixtures/clientSummary'
import { HttpMethod } from '../src/types/http'
import { json, stubApi } from './support/stubApi'

/**
 * Stubs the client the log page needs.
 *
 * @param page - The page under test.
 */
const stubClient = async (page: Page): Promise<void> => {
  await stubApi(page, {
    signedIn: true,
    routes: [
      json(HttpMethod.Get, '/api/clients/7', adaDetail),
      json(HttpMethod.Get, '/api/clients/7/summary', emptyClientSummary),
    ],
  })
}

test('the filter block closes through a height transition and is then gone', async ({ page }) => {
  await stubClient(page)
  await page.goto('/clients/7/log')
  await page.getByTestId('client-log-toggle-filters').click()
  const filters = page.getByTestId('client-log-filters')
  await expect(filters).toBeVisible()

  // Record whether the block's box ever carries the collapsing classes, so a slow machine cannot miss the 200 ms.
  await page.evaluate(() => {
    const seen = { collapsing: false, property: '' }
    Reflect.set(window, '__collapse', seen)
    new MutationObserver(() => {
      const box = document.querySelector('.ui-collapsing')
      if (box !== null) {
        seen.collapsing = true
        seen.property = getComputedStyle(box).transitionProperty
      }
    }).observe(document.body, { subtree: true, childList: true, attributes: true, attributeFilter: ['class'] })
  })

  await page.getByTestId('client-log-toggle-filters').click()

  await expect(filters).toHaveCount(0)
  const seen = await page.evaluate(() => Reflect.get(window, '__collapse') as { collapsing: boolean; property: string })
  expect(seen.collapsing).toBe(true)
  expect(seen.property).toContain('grid-template-rows')
})

test('under reduced motion the filter block is gone at once, with no transition', async ({ page }) => {
  await page.emulateMedia({ reducedMotion: 'reduce' })
  await stubClient(page)
  await page.goto('/clients/7/log')
  await page.getByTestId('client-log-toggle-filters').click()
  await expect(page.getByTestId('client-log-filters')).toBeVisible()

  await page.getByTestId('client-log-toggle-filters').click()

  await expect(page.getByTestId('client-log-filters')).toHaveCount(0)
  await expect(page.locator('.ui-collapsing')).toHaveCount(0)
})
