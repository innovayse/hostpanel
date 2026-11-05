/**
 * One plugin page in the client area's plugin index (`PluginUiPageDto`).
 *
 * @module types/pluginuipage
 */

/** One plugin page the caller may open. */
export interface PluginUiPage {
  /** The plugin id. */
  pluginId: string
  /** The page id. */
  id: string
  /**
   * The page's path template below the plugin's prefix, e.g. `/sites/{siteId}`; each `{name}` segment is a page
   * parameter, read from the address and used to fill the routes the page's blocks call.
   */
  path: string
  /** The title, in the caller's language. */
  title: string
}
