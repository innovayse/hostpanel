/**
 * Focus after the enter of a drawer and a dialog: once the animation has ended, focus is on the first control
 * (the close button), not on the panel. The close button of a drawer is invisible until the panel has settled,
 * and an invisible control cannot take focus, so this proves the move waits for it to show. jsdom has no
 * stylesheet, so only a real browser can show it.
 */
import { expect, test } from '@playwright/test'
import { HttpMethod } from '../src/types/http'
import { ApiPath } from '../src/lib/apiPaths'
import { adaDetail } from './fixtures/clients'
import { emptyClientSummary } from './fixtures/clientSummary'
import { sampleFaq } from './fixtures/storefront'
import { json, stubApi } from './support/stubApi'
import { stubCrud } from './support/stubCrud'

/** An empty page of a paged list. */
const emptyPage = { items: [], pageNumber: 1, pageSize: 5, totalCount: 0, totalPages: 0 }

test.describe('on a phone', () => {
  test.use({ viewport: { width: 390, height: 800 } })

  test('the menu drawer puts focus on its close button when it has slid in', async ({ page }) => {
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

    await page.getByTestId('topbar-toggle-sidebar').click()

    await expect(page.getByTestId('drawer-close')).toBeVisible()
    await expect(page.getByTestId('drawer-close')).toBeFocused()
  })
})

test('a dialog puts focus on its first control when it has appeared', async ({ page }) => {
  await stubCrud(page, { root: ApiPath.AdminFaqs, stored: [{ ...sampleFaq }], newId: 72 })
  await page.goto('/storefront/faqs')

  await page.getByTestId(`faq-row-${sampleFaq.id}`).getByTestId('row-actions-trigger').click()

  await page.getByTestId(`faq-delete-${sampleFaq.id}`).click()

  await expect(page.getByTestId('dialog-close')).toBeFocused()
})
