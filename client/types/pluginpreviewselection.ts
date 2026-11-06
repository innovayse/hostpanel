/**
 * The shapes of element selection on a plugin's preview.
 *
 * @module types/pluginpreviewselection
 */

import type { PluginViewRoute } from '~/types/pluginviewroute'

/** A preview block's `selection`: declared by the view, off when absent. */
export interface PluginPreviewSelection {
  /** The ids of the sources that may select; a live source stays out. */
  sources: readonly string[]
  /** The POST route that looks a claimed selection up on the plugin's server. */
  route: PluginViewRoute
  /** Where the release on the preview is read. */
  release: {
    /** The GET route. */
    route: PluginViewRoute
    /** The dotted field of its answer. */
    field: string
  }
}

/** What a frame's message may say, once it passed every check. */
export type PluginSelectionMessage =
  | { /** The frame's script runs. */ event: 'ready' }
  | { /** The reader asked to leave the mode. */ event: 'exit' }
  | { /** The reader pointed at an element. */ event: 'toggle', /** The element id, still only a claim. */ id: string }

/** A selected element as the plugin's server named it. */
export interface PluginSelectionChip {
  /** The element id. */
  id: string
  /** What the chip reads, written by the plugin's server. */
  label: string
}

/** What is sent with a message, which the plugin's server checks again. */
export interface PluginSelectionClaim {
  /** The release the elements were selected on. */
  releaseId: string
  /** The element ids. */
  ids: string[]
}
