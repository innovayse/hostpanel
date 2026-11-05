/**
 * GET /api/portal/public/config
 *
 * The configuration the header widget reads from the origin it was served from: where the sign-on
 * service is, as the browser reaches it. The widget uses it to frame the remembered-accounts list,
 * and falls back to a guess from the page's host name when this is missing, which is wrong for any
 * deployment on its own domain.
 *
 * Not under `server/api/portal/public/` with the storefront feeds: it answers from the runtime
 * configuration and never reaches the C# API, which that folder's contract test requires.
 *
 * @module server/routes/api/portal/public/config.get
 */

import { requireShellUrl } from '~/server/utils/platformShell'
import { requireSsoSetting } from '~/server/utils/requireSsoSetting'

export default defineEventHandler(() => {
  requireShellUrl()
  return { ssoUrl: requireSsoSetting(useRuntimeConfig().public.ssoPublicUrl) }
})
