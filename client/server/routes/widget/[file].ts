/**
 * GET and HEAD /widget/header.js and /widget/header.v2.js
 *
 * Relays the platform shell's header widget from this portal's own origin. The script builds every
 * account-switch, add-account and launcher address from the origin it was served from, so serving
 * it here makes those addresses this portal's own (its sign-in client, its callback, its return
 * path) and not the shell's. See `server/utils/platformShell.ts` for the reason and the cache.
 *
 * Only the two known file names are relayed. The legacy `header.js` loads `header.v2.js` from its
 * own directory, which is this route again.
 *
 * @module server/routes/widget/[file]
 */

import { PortalErrorCode } from '~/constants/portalErrorCode'
import { readWidgetScript, WIDGET_FILES } from '~/server/utils/platformShell'
import { defineReadOnlyEventHandler } from '~/server/utils/readOnlyEventHandler'

export default defineReadOnlyEventHandler(async (event) => {
  const name = getRouterParam(event, 'file') ?? ''
  if (!WIDGET_FILES.has(name)) {
    throw createError({ statusCode: 404, data: { code: PortalErrorCode.WidgetFileNotFound } })
  }

  const script = await readWidgetScript(name)
  setResponseHeaders(event, {
    'Content-Type': 'text/javascript; charset=utf-8',
    'Cache-Control': 'public, max-age=300',
    'X-Content-Type-Options': 'nosniff'
  })
  return script
})
