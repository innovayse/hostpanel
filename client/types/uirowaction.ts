/**
 * One action of a table row.
 *
 * @module types/uirowaction
 */

import type { Component } from 'vue'

/** An action a table puts in the "..." menu of a row (`UiRowActions`). */
export interface UiRowAction {
  /** Stable key of the action; also the suffix of its `data-testid`. */
  key: string
  /** Visible, already-translated label. */
  label: string
  /** Lucide icon component drawn before the label. */
  icon: Component
  /** Route to open; leave it out for an action that runs `onSelect`. */
  to?: string
  /** Runs when the action is chosen and it has no `to`. */
  onSelect?: () => void | Promise<void>
}
