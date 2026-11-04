/**
 * POST /api/portal/client/payment-methods/sessions
 * Starts saving a payment method with a gateway module and records the payer's agreement to
 * automatic renewal charges.
 *
 * Body: { gatewayModuleId: string, consentAccepted?: boolean }. The address a hosted gateway sends
 * the payer back to is built here from this portal's own origin and never read from the body, so a
 * visitor cannot point the payer at another site.
 *
 * @module server/api/portal/client/payment-methods/sessions.post
 */

import { clientAccountTabPath } from '~/constants/clientRoutes'
import { AccountTab } from '~/types/accounttab'
import { HttpMethod } from '~/types/httpmethod'
import { readBodyObject, readBodyStrings } from '~/server/utils/readBodyObject'

/** Where the payer lands after a hosted gateway: the account page on its payment tab. */
const RETURN_PATH = clientAccountTabPath(AccountTab.Payment)

export default defineEventHandler(async (event) => {
  const { gatewayModuleId } = await readBodyStrings(event, 'gatewayModuleId')
  const { consentAccepted } = await readBodyObject(event)
  const origin = useRuntimeConfig(event).public.baseUrl || getRequestURL(event).origin
  const returnUrl = new URL(`${RETURN_PATH}&module=${encodeURIComponent(gatewayModuleId)}`, origin).toString()

  return await internalApiCall<unknown>(event, '/me/payment-methods/sessions', {
    method: HttpMethod.Post,
    body: { gatewayModuleId, returnUrl, consentAccepted: consentAccepted === true },
  })
})
