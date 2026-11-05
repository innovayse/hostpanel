/**
 * The C# API's plugin endpoints of the client panel, under `/api/client/plugins`, spelled in one
 * place. They share the `/client` prefix with the portal's own pages only by coincidence of names,
 * so this module is the one server file `scripts/check-layers.mjs` lets spell it.
 *
 * Every argument is expected already validated and encoded by the route that passes it.
 *
 * @module server/utils/pluginBackendPaths
 */

/** The backend's root of the client panel's plugin endpoints, after `/api`. */
const ROOT = '/client/plugins'

/** The backend paths of the client panel's plugin endpoints. */
export const PluginBackendPaths = {
  /** The plugin index: menu items and pages. */
  ui: `${ROOT}/ui`,
  /**
   * The page view an address names, under the reserved `ui` segment: the API matches the address against the
   * plugin's page templates and answers `PluginPageViewDto` or `PLUGIN_PAGE_NOT_FOUND`.
   *
   * @param plugin - The plugin id.
   * @param path - The page address below the plugin's prefix, with its leading `/`, segments already encoded.
   * @returns The path with the address as its `path` query value.
   */
  pageByPath: (plugin: string, path: string): string =>
    `${ROOT}/ui/${plugin}/pages/by-path?${new URLSearchParams({ path }).toString()}`,
  /**
   * One slot's widget index, under the reserved `slots` segment.
   *
   * @param slot - The slot id.
   * @param query - The query string, with its `?`, or empty.
   * @returns The path.
   */
  slot: (slot: string, query: string): string => `${ROOT}/slots/${slot}${query}`,
  /**
   * One slot widget's view.
   *
   * @param slot - The slot id.
   * @param plugin - The plugin id.
   * @param widget - The widget id.
   * @param query - The query string, with its `?`, or empty.
   * @returns The path.
   */
  slotWidget: (slot: string, plugin: string, widget: string, query: string): string =>
    `${ROOT}/slots/${slot}/widgets/${plugin}/${widget}${query}`,
  /**
   * One of a plugin's own routes.
   *
   * @param plugin - The plugin id.
   * @param path - The route path below the plugin's prefix, with any query.
   * @returns The path.
   */
  route: (plugin: string, path: string): string => `${ROOT}/${plugin}/${path}`,
} as const
