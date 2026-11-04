/**
 * GET /api/portal/client/plugins/:plugin/page?path=/sites/blog
 * The plugin page an address names, with its view document, labels in the reader's language
 * (`GET /api/client/plugins/ui/{plugin}/pages/by-path?path=…`, the lookup asked of #643). The API matches the
 * address against the plugin's page templates; this route only checks the shape of what it forwards.
 *
 * @module server/api/portal/client/plugins/[plugin]/page.get
 */

import type { PluginPageView } from '~/types/pluginpageview'
import { RouteParamKind } from '~/types/routeparamkind'

export default defineEventHandler((event): Promise<PluginPageView> => {
  const plugin = requireRouteParam(event, 'plugin', RouteParamKind.PluginId)
  const path = requirePluginPagePath(event)
  return internalApiCall<PluginPageView>(event, PluginBackendPaths.pageByPath(plugin, path))
})
