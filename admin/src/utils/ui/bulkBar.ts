/**
 * Shape of one action of the floating bulk bar (`UiBulkBar`).
 */
import type { UiBarAction } from '@/utils/ui/actionBar'

/** One action of the bulk bar: a short label with an icon, and the full sentence as its tooltip. */
export interface UiBulkAction extends UiBarAction {
  /** Stable key of the action; the bar emits it when the action is chosen. */
  key: string
  /** The full action sentence (already translated): the tooltip of the button and the accessible name. */
  title: string
}
