/**
 * A condition of a view document (the SDK's `visibleWhen` and `followUp.until`, one shape for both): a GET route of
 * the plugin is read, one dotted field of its reply is taken, and the condition holds when that value strictly
 * equals (`===`, no number and string coercion) one of the listed values. The host checked the shape at install
 * (`PLUGIN_VIEW_CONDITION_INVALID`); the panel only reads it.
 *
 * @module types/pluginviewcondition
 */

import type { PluginViewRoute } from '~/types/pluginviewroute'

/** One JSON scalar a condition compares against. */
export type PluginConditionValue = string | number | boolean | null

/** A condition read from a route. */
export interface PluginViewCondition {
  /** The GET route read. */
  route: PluginViewRoute
  /** The dotted path of the field compared, e.g. `site.status`. */
  field: string
  /** The value the field must equal, or a list of values one of which it must equal. */
  equals: PluginConditionValue | readonly PluginConditionValue[]
}
