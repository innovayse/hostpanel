/**
 * The client area's plugin index, `GET /api/client/plugins/ui` (`PluginUiDto`): the menu items and pages of the
 * enabled plugins the caller may open. Both lists are empty when no plugin adds any.
 *
 * @module types/pluginui
 */

import type { PluginMenuItem } from '~/types/pluginmenuitem'
import type { PluginUiPage } from '~/types/pluginuipage'

/** The plugin index of one panel. */
export interface PluginUi {
  /** The menu items, by plugin then `order`. */
  menus: PluginMenuItem[]
  /** The pages, by plugin then manifest order. */
  pages: PluginUiPage[]
}
