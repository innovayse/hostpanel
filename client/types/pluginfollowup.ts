/**
 * What an action or a form asks the panel to do after its call succeeded, for work that ends later (the SDK's
 * `followUp`): read the named blocks again, at the host's own interval and for the host's own number of attempts,
 * until a field of a route read reaches a final value. The plugin names what to watch, never how often or how long;
 * the bounds are `constants/pluginFollowUp.ts`.
 *
 * @module types/pluginfollowup
 */

import type { PluginViewCondition } from '~/types/pluginviewcondition'

/** A follow-up refresh. */
export interface PluginFollowUp {
  /** The ids of the blocks read again on every attempt. */
  refresh: readonly string[]
  /** The condition that ends the follow-up once it holds. */
  until: PluginViewCondition
}
