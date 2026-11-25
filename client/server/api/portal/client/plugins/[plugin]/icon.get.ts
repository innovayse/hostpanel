/**
 * GET /api/portal/client/plugins/:plugin/icon
 * The icon an installed plugin ships (`GET /api/me/plugins/{id}/icon`), for the "Open panel" button of a
 * service served by that plugin. The bytes are passed on with the type the API declared and the headers that
 * keep an image from being run: `nosniff` and a policy that allows nothing but its own inline style, so an SVG
 * can never script the portal.
 *
 * @module server/api/portal/client/plugins/[plugin]/icon.get
 */

import { RouteParamKind } from '~/types/routeparamkind'

/** What the icon answers with when the API names no caching rule of its own. */
const PRIVATE_REVALIDATE = 'private, no-cache'

/** The policy an icon is served under: no script, no fetch, only the inline style an SVG may carry. */
const ICON_POLICY = "default-src 'none'; style-src 'unsafe-inline'"

export default defineEventHandler(async (event) => {
  const plugin = requireRouteParam(event, 'plugin', RouteParamKind.PluginId)

  const file = await internalApiBinaryCall(event, `/me/plugins/${plugin}/icon`)
  setResponseHeaders(event, {
    'content-type': file.contentType,
    'cache-control': file.cacheControl ?? PRIVATE_REVALIDATE,
    'x-content-type-options': 'nosniff',
    'content-security-policy': ICON_POLICY,
  })
  return file.body
})
