/**
 * What a rendered plugin view hands the blocks below it: the plugin, the page parameter values the blocks' routes
 * are filled from, the plugin's allowed link origins, and the refresh signals actions send to other blocks.
 *
 * @module types/pluginviewcontext
 */

import type { Ref } from 'vue'
import type { PluginPageLink } from '~/types/pluginblock'

/** The context of one rendered plugin view. */
export interface PluginViewContext {
  /** The plugin id. */
  pluginId: string
  /** The page parameter values by name, read from the address. */
  values: Record<string, string>
  /** The plugin's allowed link origins. */
  externalOrigins: string[]
  /** The exact origins a preview block may frame; empty outside a plugin page. */
  frameOrigins: string[]
  /** A counter per block id, bumped to make that block read its source again. */
  refreshes: Ref<Record<string, number>>
  /** Bumps the counters of the named blocks. */
  refresh: (ids: string[]) => void
  /** Opens another page of the plugin, its parameters read from a row or reply. */
  open: (link: PluginPageLink, row: unknown) => void
  /** Told of each block's first read as it starts, so a slot widget can tell a slow widget from a ready one. */
  track: (read: Promise<void>) => void
  /**
   * Puts a running follow-up in the view's keeping: the view ends it when the view is left, and not before, so a block
   * that hides itself while its own work runs cannot end the watch it started. A watch adopted under a key an earlier
   * one still holds ends that earlier one.
   *
   * @param key - Who the watch belongs to: the starting block's id.
   * @param stop - Ends the watch.
   * @returns Lets go of the watch once it has ended on its own; a later watch under the same key is not touched.
   */
  adopt: (key: string, stop: () => void) => () => void
}
