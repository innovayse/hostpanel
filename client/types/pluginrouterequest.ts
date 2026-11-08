/**
 * The request a view's route reference comes to once the page can fill it: which method, and which address below the
 * plugin's routes. Two references with the same request are the same call, whoever made them.
 *
 * @module types/pluginrouterequest
 */

import type { HttpMethod } from '~/types/httpmethod'

/** One call of a plugin route, as it would be sent. */
export interface PluginRouteRequest {
  /** The route's method. */
  method: HttpMethod
  /** The route's path below the plugin's routes, parameters filled and encoded, with the query when there is one. */
  url: string
}
