/**
 * Reduced motion: with the operating system asking for it, a dialog opens and closes without moving. The
 * unit tests read the class strings; this run reads what the browser computes from them, so a `motion-reduce:`
 * partner that Tailwind did not generate would fail here.
 */
import { expect, test } from '@playwright/test'
import { ApiPath } from '../src/lib/apiPaths'
import { sampleFaq } from './fixtures/storefront'
import { stubCrud } from './support/stubCrud'

test('a dialog shows and goes away without a transition or a transform when reduced motion is asked for', async ({ page }) => {
  await page.emulateMedia({ reducedMotion: 'reduce' })
  await stubCrud(page, { root: ApiPath.AdminFaqs, stored: [{ ...sampleFaq }], newId: 72 })
  await page.goto('/storefront/faqs')
  await expect(page.getByTestId(`faq-row-${sampleFaq.id}`)).toBeVisible()

  await page.getByTestId(`faq-actions-${sampleFaq.id}`).click()

  await page.getByTestId(`faq-delete-${sampleFaq.id}`).click()

  const panel = page.getByTestId('dialog-panel')
  const backdrop = page.getByTestId('dialog-backdrop')
  await expect(panel).toBeVisible()
  // The panel's own movement is off: no transition property, and the identity transform.
  expect(await panel.evaluate((node) => getComputedStyle(node).transitionProperty)).toBe('none')
  expect(await panel.evaluate((node) => getComputedStyle(node).transform)).toBe('none')
  // The backdrop around it does not fade either: it is fully opaque and either has no transition property (the enter class is still on) or no time (it is gone).
  const backdropStyle = await backdrop.evaluate((node) => {
    const style = getComputedStyle(node)
    return { opacity: style.opacity, moves: style.transitionProperty !== 'none' && style.transitionDuration !== '0s' }
  })
  expect(backdropStyle).toEqual({ opacity: '1', moves: false })

  await page.getByTestId('dialog-cancel').click()
  await expect(page.getByTestId('dialog-body')).toHaveCount(0)
})

test.describe('without the preference', () => {
  test('the same panel does carry a transition, so the reduced run above proves something', async ({ page }) => {
    await stubCrud(page, { root: ApiPath.AdminFaqs, stored: [{ ...sampleFaq }], newId: 72 })
    await page.goto('/storefront/faqs')
    await page.getByTestId(`faq-actions-${sampleFaq.id}`).click()
    await page.getByTestId(`faq-delete-${sampleFaq.id}`).click()

    const panel = page.getByTestId('dialog-panel')
    await expect(panel).toBeVisible()
    expect(await panel.evaluate((node) => getComputedStyle(node).transitionProperty)).not.toBe('none')
    // The kit's `--duration-base` (200ms) reaches the browser as a real, non-zero duration, not a dropped `var()`.
    expect(await panel.evaluate((node) => getComputedStyle(node).transitionDuration)).toBe('0.2s')
  })
})
