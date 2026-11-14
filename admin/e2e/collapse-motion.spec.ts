/**
 * Collapsible blocks: a block that opens or closes keeps its height animating (it is still in the page just after
 * the click), then is gone, and under reduced motion it is gone at once. Run on the create invoice form of a client's invoices tab.
 */
import { expect, test } from '@playwright/test'
import type { Page } from '@playwright/test'
import { adaDetail } from './fixtures/clients'
import { emptyClientInvoices } from './fixtures/billing'
import { HttpMethod } from '../src/types/http'
import { json, stubApi } from './support/stubApi'

/**
 * Stubs the client and the empty invoice list the invoices tab needs.
 *
 * @param page - The page under test.
 */
const stubClient = async (page: Page): Promise<void> => {
  await stubApi(page, {
    signedIn: true,
    routes: [
      json(HttpMethod.Get, '/api/clients/7', adaDetail),
      json(HttpMethod.Get, '/api/billing/client/7', emptyClientInvoices),
    ],
  })
}

test('the create invoice form closes through a height transition and is then gone', async ({ page }) => {
  await stubClient(page)
  await page.goto('/clients/7/invoices')
  await page.getByTestId('invoice-create-toggle').click()
  const form = page.getByTestId('invoice-create-form')
  await expect(form).toBeVisible()

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

  await page.getByTestId('invoice-create-toggle').click()

  await expect(form).toHaveCount(0)
  const seen = await page.evaluate(() => Reflect.get(window, '__collapse') as { collapsing: boolean; property: string })
  expect(seen.collapsing).toBe(true)
  expect(seen.property).toContain('grid-template-rows')
})

test('under reduced motion the create invoice form is gone at once, with no transition', async ({ page }) => {
  await page.emulateMedia({ reducedMotion: 'reduce' })
  await stubClient(page)
  await page.goto('/clients/7/invoices')
  await page.getByTestId('invoice-create-toggle').click()
  await expect(page.getByTestId('invoice-create-form')).toBeVisible()

  await page.getByTestId('invoice-create-toggle').click()

  await expect(page.getByTestId('invoice-create-form')).toHaveCount(0)
  await expect(page.locator('.ui-collapsing')).toHaveCount(0)
})
