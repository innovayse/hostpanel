/**
 * One row of a plugin chat reply's step list.
 *
 * @module types/pluginchatstep
 */

import type { PluginChatStepState } from '~/types/pluginchatstepstate'

/** One step. */
export interface PluginChatStep {
  /** The event's `id`, or else its label; a repeat updates the row. */
  key: string
  /** The text, rendered as text. */
  label: string
  /** The state. */
  state: PluginChatStepState
}
