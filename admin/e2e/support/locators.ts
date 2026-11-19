/**
 * Locator helpers shared by the specs. Selectors are `data-testid` only.
 */
import type { Locator, Page } from '@playwright/test'

/**
 * The native `input`/`textarea` behind a test id.
 *
 * The `Ui*` kit may put a `data-testid` on the wrapper element or forward it to the control
 * itself, depending on the component; this resolves to the control either way, so a spec does
 * not break when a field moves between a bare element and a kit component.
 *
 * @param page - The page under test.
 * @param testId - The `data-testid` of the field.
 * @returns A locator for the single editable control.
 */
export const fieldOf = (page: Page, testId: string): Locator =>
  page.getByTestId(testId).locator('input, textarea').or(
    page.locator(`input[data-testid="${testId}"], textarea[data-testid="${testId}"]`),
  )

/**
 * Measures an element that must be on screen.
 *
 * @param locator - The element.
 * @returns Its bounding box.
 * @throws When the element is not rendered, so a missing element fails the test with its name instead of a null read.
 */
export const boxOf = async (locator: Locator): Promise<{ x: number, y: number, width: number, height: number }> => {
  const box = await locator.boundingBox()
  if (box === null) {
    throw new Error(`${String(locator)} has no box: it is not rendered`)
  }
  return box
}

/**
 * One tab's panel on the system settings screen.
 *
 * Every panel stays mounted and only the selected one shows, so a test id such as the Save bar's
 * exists once per tab; scoping to the panel keeps a locator to one element.
 *
 * @param page - The page under test.
 * @param tab - The tab's value in the address (`general`, `appearance`, `integrations`, `advanced`).
 * @returns A locator for the panel.
 */
export const settingsPanel = (page: Page, tab: string): Locator => page.getByTestId(`settings-panel-${tab}`)

/**
 * The toast that carries a sentence.
 *
 * A success is confirmed in a self-dismissing toast drawn by `UiToastHost`, not in an inline banner; several toasts can be on
 * screen at once, so the locator narrows to the one holding the text.
 *
 * @param page - The page under test.
 * @param text - The sentence, or a part of it, the toast must show.
 * @returns A locator for the toast with that text.
 */
export const toastWith = (page: Page, text: string): Locator => page.getByTestId('ui-toast').filter({ hasText: text })
