/**
 * Endpoints behind the integrations store: the integration list, per-integration config, the
 * connection test.
 *
 * No state lives here; every function rejects with an `ApiError` carrying the problem+json body.
 */

import { useApi } from '@/composables/useApi'
import type {
  IntegrationConfigPayload,
  IntegrationDetailWireDto,
  IntegrationWireDto,
  IntegrationTestResult,
} from '@/types/integration'
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
   * @returns The summary rows, with the backend's category strings and the null name of a built-in integration untouched.
   */
  const listIntegrations = (): Promise<IntegrationWireDto[]> =>
    request<IntegrationWireDto[]>(ApiPath.AdminIntegrations)

  /**
   * Reads one integration's config.
   *
   * @param slug - Integration slug.
   * @returns The detail with its field definitions and values.
   */
  const getIntegration = (slug: string): Promise<IntegrationDetailWireDto> =>
    request<IntegrationDetailWireDto>(`${ApiPath.AdminIntegrations}/${slug}`)

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

  return {
    listIntegrations,
    getIntegration,
    saveIntegration,
    testIntegration,
  }
}
