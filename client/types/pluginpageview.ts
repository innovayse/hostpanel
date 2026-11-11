/**
 * One plugin page with its view document, `GET /api/client/plugins/ui/{plugin}/pages/{page}` (`PluginPageViewDto`):
 * every label already in the caller's language.
 *
 * @module types/pluginpageview
 */

import type { PluginView } from '~/types/pluginview'

/** One plugin page view. */
export interface PluginPageView {
  /** The plugin id. */
  pluginId: string
  /** The page id. */
  id: string
  /** The page's path template below the plugin's prefix, as in the index. */
  path: string
  /** The title, in the caller's language. */
  title: string
  /** The web origins the plugin's absolute links may open (the manifest's `externalOrigins` plus the https origins of the reader's own bound services); empty allows host-relative links only. */
  externalOrigins: string[]
  /** How the page is drawn, `PluginPageLayout` as a wire string; anything unknown reads as standard. */
  layout: string
  /** The exact origins a preview block may frame, computed by the host per caller; never read from a view or a reply. */
  frameOrigins: string[]
  /** The view document. */
  view: PluginView
}
