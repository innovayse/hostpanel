/**
 * Flow: focus ring.
 *
 * A mouse click on a link must leave no outline behind; the ring belongs to keyboard focus only.
 */
import { expect, test } from './support/test'
import { waitForHydration } from './support/helpers'

test('clicking a link leaves no focus outline or ring', async ({ page }) => {
  await page.goto('/faq')
  await waitForHydration(page)

  const link = page.getByTestId('faq-category-general')
  await link.click()
  await expect(page).toHaveURL(/\/faq\/general$/)

  // Press the mouse without releasing it: the press is what focuses the link, and only the
  // release would navigate. A full click() leaves /faq/general, so the link is gone from the
  // page before its style can be read and the evaluate below waits until the test times out.
  const back = page.getByTestId('faq-back-to-all')
  // The link sits under the accordion, below the fold; the mouse cannot press what is off
  // screen, so bring it into view before reading where it is.
  await back.scrollIntoViewIfNeeded()
  const box = await back.boundingBox()
  expect(box).not.toBeNull()
  if (!box) {
    return
  }
  await page.mouse.move(box.x + box.width / 2, box.y + box.height / 2)
  await page.mouse.down()
  await expect(back).toBeFocused()
  const style = await back.evaluate(el => ({ outline: getComputedStyle(el).outlineStyle, shadow: getComputedStyle(el).boxShadow }))
  await page.mouse.up()
  expect(style.outline).toBe('none')
  expect(style.shadow).toBe('none')
})
