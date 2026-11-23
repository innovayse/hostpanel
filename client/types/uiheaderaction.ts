/**
 * One action of a page header.
 *
 * @module types/uiheaderaction
 */

import type { Component } from 'vue'

/**
 * An action a screen puts in its page header. The header draws it as a button from `sm` up and,
 * on a phone, folds every action into one icon button that opens a menu with all of them.
 */
export interface UiHeaderAction {
  /** Stable key of the action; also the suffix of its `data-testid`. */
  key: string
  /** Visible, already-translated label; the menu row on a phone shows it in full. */
  label: string
  /** Lucide icon component drawn before the label. */
  icon: Component
  /** Route to open; leave it out for an action that runs `onSelect`. */
  to?: string
  /** Runs when the action is chosen and it has no `to`. */
  onSelect?: () => void | Promise<void>
  /** The main, create-like action: drawn as the filled button, listed first in the menu and turning the phone trigger into a plus. */
  primary?: boolean
  /** Shows a spinner on the button and keeps it from being chosen again meanwhile. */
  loading?: boolean
  /** `data-testid` of the button; the menu row adds the `-menu` suffix. */
  testid: string
}
