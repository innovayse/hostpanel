/**
 * One plugin menu entry of the client sidebar, resolved to the page address it opens.
 *
 * @module types/pluginmenulink
 */

/** One plugin menu entry resolved to the page address it opens. */
export interface PluginMenuLink {
  /** The label, in the reader's language. */
  label: string
  /** The icon name the plugin chose, or null. */
  icon: string | null
  /** The page address. */
  url: string
}
