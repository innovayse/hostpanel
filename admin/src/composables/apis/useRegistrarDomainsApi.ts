/**
 * Endpoints behind the registrar domain import: the domains a registrar plugin reports, and the import of one into a client.
 *
 * No state lives here; every function rejects with an `ApiError` carrying the problem+json body.
 */

import { buildQuery, useApi } from '@/composables/useApi'
import { ApiPath } from '@/lib/apiPaths'
import { HttpMethod } from '@/types/http'
import type { ImportRegistrarDomainPayload, RegistrarDomainPage } from '@/types/registrardomain'

/**
 * Endpoint functions for `/domains/registrar`.
 *
 * @returns One typed function per endpoint.
 */
export const useRegistrarDomainsApi = () => {
  const { request } = useApi()

  /**
   * Reads one page of the domains a registrar plugin reports for its account.
   *
   * @param module - The id of the registrar plugin.
   * @param cursor - The cursor of the page wanted, as the previous page returned it; null reads the first page.
   * @returns The page, each domain marked with whether the panel has it.
   */
  const listDomains = (module: string, cursor: string | null): Promise<RegistrarDomainPage> =>
    request<RegistrarDomainPage>(`${ApiPath.Domains}/registrar/${encodeURIComponent(module)}${buildQuery({ cursor })}`)

  /**
   * Imports one domain the registrar holds into a client, without an order, an invoice or a charge.
   *
   * @param module - The id of the registrar plugin that holds the domain.
   * @param payload - The client and the domain name.
   * @returns Nothing.
   */
  const importDomain = (module: string, payload: ImportRegistrarDomainPayload): Promise<void> =>
    request<void>(`${ApiPath.Domains}/registrar/${encodeURIComponent(module)}/import`, {
      method: HttpMethod.Post,
      body: JSON.stringify(payload),
    })

  return { listDomains, importDomain }
}
