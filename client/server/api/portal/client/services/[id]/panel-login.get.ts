/**
 * GET /api/portal/client/services/:id/panel-login
 *
 * Returns how the client signs in by hand to the panel of one of their services (the panel's own sign in page, the user name,
 * the domain and the server), read from the C# backend. It is what the Open panel button shows when the serving plugin cannot
 * issue a single sign on address. The answer never carries a password.
 *
 * @module server/api/portal/client/services/[id]/panel-login.get
 */

import type { PanelLoginDetails } from '~/types/panellogindetails'

export default defineEventHandler(async (event): Promise<PanelLoginDetails> => {
  const id = requireRouteParam(event, 'id')

  return await internalApiCall<PanelLoginDetails>(event, `/me/services/${id}/panel-login`)
})
