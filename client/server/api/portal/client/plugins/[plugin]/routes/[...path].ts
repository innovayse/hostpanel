/**
 * /api/portal/client/plugins/:plugin/routes/** (GET, POST, PUT, PATCH, DELETE)
 * Calls one of a plugin's JSON routes (`/api/client/plugins/{plugin}/{path}`) with the request's
 * query and JSON body. The backend matches the path against the plugin's declared routes and
 * authorises the call; this route only checks the path's shape and forwards.
 *
 * @module server/api/portal/client/plugins/[plugin]/routes/[...path]
 */

import { HttpMethod } from '~/types/httpmethod'
import { RouteParamKind } from '~/types/routeparamkind'

/** The methods a plugin route may declare. */
const ROUTE_METHODS: ReadonlySet<string> = new Set([HttpMethod.Get, HttpMethod.Post, HttpMethod.Put, HttpMethod.Patch, HttpMethod.Delete])

export default defineEventHandler(async (event): Promise<unknown> => {
  if (!ROUTE_METHODS.has(event.method)) {
    throw createError({ statusCode: 405 })
  }
  const plugin = requireRouteParam(event, 'plugin', RouteParamKind.PluginId)
  const path = requirePluginPath(event)
  const method = event.method as HttpMethod
  const body = method === HttpMethod.Get || method === HttpMethod.Delete ? undefined : await readBodyObject(event)
  const reply = await internalApiCall<unknown>(event, PluginBackendPaths.route(plugin, `${path}${pluginQuery(event)}`), { method, body })
  // A 204 reads as an empty body; the browser gets JSON null rather than an empty answer.
  return reply ?? null
})
