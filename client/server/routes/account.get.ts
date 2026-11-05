/**
 * GET /account
 *
 * The "Manage all services" link of the header widget points at `<this origin>/account`. The
 * account centre belongs to the platform shell, so the visitor is sent there. A host that routes
 * `/account` elsewhere before it reaches this server never gets here.
 *
 * @module server/routes/account.get
 */

import { ACCOUNT_APP_PATH } from '~/constants/clientRoutes'
import { requireShellUrl } from '~/server/utils/platformShell'

export default defineEventHandler((event) => {
  return sendRedirect(event, `${requireShellUrl()}${ACCOUNT_APP_PATH}`, 302)
})
