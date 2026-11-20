/**
 * Types and key handling shared by `UiTabs` and `UiSegmented`.
 */
import { Key } from '@/utils/ui/keys'

/** The small dot a tab can carry beside its caption: a value still missing, or one the server refused. */
export const UiTabMarker = {
  Empty: 'empty',
  Invalid: 'invalid',
} as const

/** One of the tab markers. */
export type UiTabMarker = (typeof UiTabMarker)[keyof typeof UiTabMarker]

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
  /** Dot drawn beside the caption (`UiTabs` only); absent draws none. */
  marker?: UiTabMarker
  /** Accessible text of the dot (already translated); the dot is hidden from assistive tech without it. */
  markerLabel?: string
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

/**
 * Turns the tabs of a skeleton schema into the items of a tab strip, captions translated.
 *
 * @param tabs - The schema's tabs: a key and a locale key of the caption each.
 * @param translate - Resolves a locale key into the operator's language.
 * @param testidPrefix - When given, each item's `data-testid` is this prefix and its key.
 * @returns The items for `UiTabs`.
 */
export const tabItemsOfSchema = (
  tabs: readonly { key: string; label: string }[],
  translate: (key: string) => string,
  testidPrefix?: string,
): UiTabItem[] =>
  tabs.map((tab) => ({ value: tab.key, label: translate(tab.label), testid: testidPrefix === undefined ? undefined : `${testidPrefix}${tab.key}` }))
