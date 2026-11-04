/**
 * A reference from a view document to one route of the plugin (the SDK's `PluginViewRoute`): routes have no ids,
 * so a block names one by its method and its declared path template, e.g.
 * `{ "method": "post", "path": "/services/{serviceId}/site/publish" }`. The panel fills each `{parameter}` from the
 * page parameter of the same name and calls the route through the host, which checks it against the manifest.
 *
 * @module types/pluginviewroute
 */

/** One route reference of a view. */
export interface PluginViewRoute {
  /** The route's method as the manifest spells it (`get`, `post`, `put`, `patch`, `delete`); compared case-blind. */
  method: string
  /** The path template exactly as the route declares it, parameter names included. */
  path: string
}
