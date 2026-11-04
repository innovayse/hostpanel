/**
 * One menu item a plugin adds to the client area, as `GET /api/client/plugins/ui` sends it (`PluginUiMenuItemDto`):
 * already filtered to what the caller may open and labelled in the caller's language.
 *
 * @module types/pluginmenuitem
 */

/** One plugin menu item. */
export interface PluginMenuItem {
  /** The plugin id. */
  pluginId: string
  /** The item id, unique within the plugin. */
  id: string
  /** The label, in the caller's language. */
  label: string
  /** The id of the plugin page the item opens. */
  page: string
  /** The name of an icon from the host's fixed plugin icon set, or null for the default icon. */
  icon: string | null
  /** The position among the items, lowest first. */
  order: number
}
