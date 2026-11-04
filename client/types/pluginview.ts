/**
 * A plugin view document: the blocks of a page or a slot widget.
 *
 * @module types/pluginview
 */

import type { PluginBlock } from '~/types/pluginblock'

/** A plugin view document. */
export interface PluginView {
  /** The schema version, 1. */
  schema: number
  /** The blocks, in display order. */
  blocks: PluginBlock[]
}
