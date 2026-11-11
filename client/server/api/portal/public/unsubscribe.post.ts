/**
 * POST /api/portal/public/unsubscribe
 *
 * Forwards the token of a campaign mail's unsubscribe link to the C# API. Anonymous (the reader
 * follows the link from a mail, with no session), routed through `internalApiCall` so
 * `X-Language` is forwarded and a refusal keeps the backend's `detail`. A missing or blank token is
 * refused by the backend's validator, so there is no pre-check, and no sentence, here.
 *
 * The success body is empty (204) and parses to an empty string: hence the `string` type argument;
 * the one caller (`useUnsubscribeApi.unsubscribe`) reads nothing from it.
 *
 * @module server/api/portal/public/unsubscribe.post
 */

import { HttpMethod } from '~/types/httpmethod'
import { readBodyObject } from '~/server/utils/readBodyObject'

export default defineEventHandler(async (event) => {
  const body = await readBodyObject(event)
  const token = typeof body.token === 'string' ? body.token.trim() : ''

  return await internalApiCall<string>(event, '/public/unsubscribe', {
    method: HttpMethod.Post,
    body: { token },
    anonymous: true
  })
})
