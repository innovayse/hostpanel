/**
 * What the plugins add to the admin panel: the index behind the Add-ons menu and the page a plugin
 * declares (`GET /api/admin/plugins/ui`, `.../ui/{pluginId}/pages/by-path`).
 *
 * The host has already kept only the plugins that are loaded, enabled and configured, resolved every
 * label in the operator's language and put the menu in order, so the admin neither filters nor translates.
 */

import type { PluginView } from '@/types/pluginview'

/** One menu item a plugin declares for the admin panel (`PluginUiMenuItemDto`). */
export interface PluginUiMenuItemDto {
  /** Id of the plugin. */
  pluginId: string
  /** Id of the item inside its plugin. */
  id: string
  /** The label, already in the operator's language. */
  label: string
  /** Id of the page the item opens. */
  page: string
  /** A plain icon name the panel maps to a built-in icon; null for the default. */
  icon: string | null
  /** Position in the menu. */
  order: number
}

/** One page a plugin declares for the admin panel (`PluginUiPageDto`). */
export interface PluginUiPageDto {
  /** Id of the plugin. */
  pluginId: string
  /** Id of the page inside its plugin. */
  id: string
  /** The path template below the plugin's prefix, e.g. `/sites/{siteId}`. */
  path: string
  /** The title, already in the operator's language. */
  title: string
}

/** The plugin ui index (`PluginUiDto`): the menu items and the pages, empty with no available plugin. */
export interface PluginUiDto {
  /** The menu items. */
  menus: PluginUiMenuItemDto[]
  /** The pages. */
  pages: PluginUiPageDto[]
}

/** One page with its view document (`PluginPageViewDto`). */
export interface PluginPageViewDto {
  /** Id of the plugin. */
  pluginId: string
  /** Id of the page. */
  id: string
  /** The path template the address matched. */
  path: string
  /** The title, already in the operator's language. */
  title: string
  /** The view document, its labels resolved. */
  view: PluginView
  /** The only origins an absolute link of the page may open. */
  externalOrigins: string[]
}

/** One Add-ons entry of the sidebar, resolved from a menu item and its page. */
export interface PluginMenuLink {
  /** A key unique in the menu: the plugin and item ids. */
  key: string
  /** The label, already in the operator's language. */
  label: string
  /** The icon name the plugin chose, or null. */
  icon: string | null
  /** The admin address of the page. */
  to: string
}
