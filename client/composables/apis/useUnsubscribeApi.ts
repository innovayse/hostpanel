/**
 * Endpoint behind the unsubscribe page: `/api/portal/public/unsubscribe`.
 *
 * Stateless by contract: no `ref`, no `computed`. What the page shows is `stores/unsubscribe.ts`'s
 * business; this file only knows which URL answers.
 *
 * @module composables/apis/useUnsubscribeApi
 */

import { apiFetch } from '~/composables/useApi'
import { HttpMethod } from '~/types/httpmethod'

/**
 * The unsubscribe surface, one function per endpoint.
 *
 * @returns The unsubscribe endpoint functions.
 */
export const useUnsubscribeApi = () => {
  /**
   * Stops campaign mail to the address a mail's unsubscribe link belongs to.
   *
   * @param token - Token from the link, empty when the link carried none.
   * @returns Whatever the endpoint answers with (nothing, on success).
   * @throws Whatever `apiFetch` throws: a refused token is an error here, not a value.
   */
  const unsubscribe = (token: string): Promise<unknown> =>
    apiFetch('/api/portal/public/unsubscribe', { method: HttpMethod.Post, body: { token } })

  return { unsubscribe }
}
