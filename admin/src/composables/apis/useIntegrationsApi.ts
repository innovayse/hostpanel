/**
 * Endpoints behind the integrations store: the integration list, per-integration config, the
 * connection test and the live status of the CWP server.
 *
 * No state lives here; every function rejects with an `ApiError` carrying the problem+json body.
 */

import { useApi } from '@/composables/useApi'
import type {
  IntegrationConfigPayload,
  IntegrationDetailDto,
  IntegrationDto,
  IntegrationTestResult,
} from '@/types/integration'
import type { CwpServerInfoDto } from '@/types/cwp'
import { ApiPath } from '@/lib/apiPaths'
import { HttpMethod } from '@/types/http'

/**
 * Endpoint functions for `/admin/integrations`.
 *
 * @returns One typed function per endpoint.
 */
export const useIntegrationsApi = () => {
  const { request } = useApi()

  /**
   * Lists the integrations.
   *
   * @returns The summary rows, with the backend's category strings untouched.
   */
  const listIntegrations = (): Promise<IntegrationDto[]> =>
    request<IntegrationDto[]>(ApiPath.AdminIntegrations)

  /**
   * Reads one integration's config.
   *
   * @param slug - Integration slug.
   * @returns The detail with its field definitions and values.
   */
  const getIntegration = (slug: string): Promise<IntegrationDetailDto> =>
    request<IntegrationDetailDto>(`${ApiPath.AdminIntegrations}/${slug}`)

  /**
   * Saves an integration's config.
   *
   * @param slug - Integration slug.
   * @param payload - Enabled state and config values.
   * @returns Nothing; the API answers 204 No Content.
   */
  const saveIntegration = (slug: string, payload: IntegrationConfigPayload): Promise<void> =>
    request<void>(`${ApiPath.AdminIntegrations}/${slug}`, { method: HttpMethod.Put, body: JSON.stringify(payload) })

  /**
   * Runs a live connection test.
   *
   * @param slug - Integration slug.
   * @returns The probe result.
   */
  const testIntegration = (slug: string): Promise<IntegrationTestResult> =>
    request<IntegrationTestResult>(`${ApiPath.AdminIntegrations}/${slug}/test`, { method: HttpMethod.Post })

  /**
   * Reads the live status of the connected CWP server.
   *
   * @returns The server status.
   */
  const getCwpServerInfo = (): Promise<CwpServerInfoDto> =>
    request<CwpServerInfoDto>(`${ApiPath.AdminIntegrations}/cwp/server-info`)

  return {
    listIntegrations,
    getIntegration,
    saveIntegration,
    testIntegration,
    getCwpServerInfo,
  }
}
