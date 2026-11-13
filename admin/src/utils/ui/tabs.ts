/**
 * Types and key handling shared by `UiTabs` and `UiSegmented`.
 */
import { Key } from '@/utils/ui/keys'

/** One tab of a `UiTabs` strip, or one option of a `UiSegmented` control. */
export interface UiTabItem {
  /** Value the tab stands for; becomes the model when selected. */
  value: string
  /** Visible caption (already translated). */
  label: string
  /** Blocks selecting the tab. */
  disabled?: boolean
  /** `data-testid` of the tab button. */
  testid?: string
}

/**
 * Finds the enabled item an arrow key moves the selection to, wrapping at both ends.
 *
 * @param items - All items in order.
 * @param index - Index of the item that has focus.
 * @param key - The key pressed; only Left and Right move.
 * @returns The value to select, or undefined when the key does not move.
 */
export const neighbourValue = (items: UiTabItem[], index: number, key: string): string | undefined => {
  if (key !== Key.ArrowRight && key !== Key.ArrowLeft) {
    return undefined
  }
  const step = key === Key.ArrowRight ? 1 : -1
  const enabled = items.filter(item => item.disabled !== true)
  const current = items[index]
  const position = current === undefined ? -1 : enabled.indexOf(current)
  return enabled[(position + step + enabled.length) % enabled.length]?.value
}
