/**
 * One function per endpoint on a client's credit (`/api/clients/{id}/credit`): the balance with its history, and the adjustment
 * staff make by hand. No state lives here, no error handling.
 *
 * The caller is the client credit store, never a `.vue` file.
 */

import { buildQuery, useApi } from '@/composables/useApi'
import { ApiPath } from '@/lib/apiPaths'
import type { AdjustCreditPayload, ClientCredit } from '@/types/clientcredit'
import { HttpMethod } from '@/types/http'

/**
 * Endpoint functions for a client's credit.
 *
 * @returns One typed function per credit endpoint the admin panel uses.
 */
export const useClientCreditApi = () => {
  /** Transport call of the shared API module. */
  const { request } = useApi()

  /**
   * Reads a client's credit: the balance and one page of the history, newest first.
   *
   * @param clientId - The client.
   * @param page - Page number (1-based).
   * @param pageSize - History lines per page.
   * @returns The credit.
   */
  const fetchCredit = (clientId: number | string, page: number, pageSize: number): Promise<ClientCredit> =>
    request<ClientCredit>(`${ApiPath.Clients}/${clientId}/credit${buildQuery({ page, pageSize })}`)

  /**
   * Adds credit to a client's balance or takes it off, with a reason.
   *
   * @param clientId - The client.
   * @param payload - The direction, the amount and the reason.
   * @returns Nothing.
   */
  const adjust = (clientId: number | string, payload: AdjustCreditPayload): Promise<void> =>
    request<void>(`${ApiPath.Clients}/${clientId}/credit/adjustments`, {
      method: HttpMethod.Post,
      body: JSON.stringify(payload),
    })

  return { fetchCredit, adjust }
}
