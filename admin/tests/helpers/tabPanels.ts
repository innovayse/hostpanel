/**
 * Helper for tabbed screens: opens each tab in turn and asserts that only the panel of the open tab is
 * drawn, so a section that belongs to one tab can never leak into another.
 */
import { expect } from 'vitest'
import { flushPromises } from '@vue/test-utils'
import type { VueWrapper } from '@vue/test-utils'

/** One tab of a screen: the test id of its button and the test id of the panel it shows. */
export interface TabPanel {
  /** Test id of the tab button. */
  tab: string
  /** Test id of the panel the tab shows. */
  panel: string
}

/**
 * Opens every tab and asserts that its own panel is drawn and that no other tab's panel is, nor any of
 * the `foreign` test ids (content that belongs to one tab only).
 *
 * @param wrapper - The mounted screen.
 * @param tabs - Every tab with its panel.
 * @param foreign - Test ids of sections that must be drawn on their own tab only, with the tab that owns them.
 */
export const expectOnlyActivePanel = async (
  wrapper: VueWrapper,
  tabs: TabPanel[],
  foreign: { owner: string, testid: string }[] = [],
): Promise<void> => {
  for (const open of tabs) {
    await wrapper.get(`[data-testid="${open.tab}"]`).trigger('click')
    await flushPromises()

    expect(wrapper.find(`[data-testid="${open.panel}"]`).exists()).toBe(true)
    for (const other of tabs.filter((candidate) => candidate.panel !== open.panel)) {
      expect(wrapper.find(`[data-testid="${other.panel}"]`).exists()).toBe(false)
    }
    for (const section of foreign) {
      expect(wrapper.find(`[data-testid="${section.testid}"]`).exists()).toBe(section.owner === open.tab)
    }
  }
}
