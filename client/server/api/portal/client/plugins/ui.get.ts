/**
 * GET /api/portal/client/plugins/ui
 * The plugin menu items and pages of the client area the signed-in person may open, labels in
 * their language, exactly as the backend sends them (`GET /api/client/plugins/ui`).
 *
 * @module server/api/portal/client/plugins/ui.get
 */

import type { PluginUi } from '~/types/pluginui'

export default defineEventHandler((event): Promise<PluginUi> =>
  internalApiCall<PluginUi>(event, PluginBackendPaths.ui))
