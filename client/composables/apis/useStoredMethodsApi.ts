/**
 * Endpoints for payment methods stored for later charges, under
 * `/api/portal/client/stored-payment-methods`.
 *
 * Every function returns a plain `Promise`; nothing is cached here.
 *
 * @module composables/apis/useStoredMethodsApi
 */

import { apiFetch } from '~/composables/useApi'
import { HttpMethod } from '~/types/httpmethod'
import type { ConsentTextVersion } from '~/types/consenttextversion'
import type { SaveMethodSession } from '~/types/savemethodsession'
import type { StoredMethod } from '~/types/storedmethod'

/** Base path of the stored-method endpoints. */
const BASE = '/api/portal/client/stored-payment-methods'

/**
 * The stored-method surface, one function per endpoint.
 *
 * @returns The endpoint functions.
 */
export const useStoredMethodsApi = () => {
  /**
   * Reads the client's stored methods.
   *
   * @returns The stored methods; an unavailable detail is null or 0 and `detailsAvailable` is false.
   * @throws Whatever `apiFetch` throws.
   */
  const fetchStoredMethods = async (): Promise<StoredMethod[]> => {
    const list = await apiFetch<StoredMethod[]>(BASE)
    return Array.isArray(list) ? list : []
  }

  /**
   * Opens a save session and records the client's consent to later charges.
   *
   * @param gatewayModuleId - Gateway module to store the method at.
   * @param consentAccepted - Whether the consent box was ticked; the API refuses a session without it.
   * @param consentTextVersion - Which wording the payer ticked; the API binds it as `consentTextVersion`, keeps it as
   * evidence and refuses a session without it.
   * @param returnUrl - Absolute URL the gateway returns the payer to.
   * @returns The session.
   * @throws Whatever `apiFetch` throws.
   */
  const beginSave = (gatewayModuleId: string, consentAccepted: boolean, consentTextVersion: ConsentTextVersion, returnUrl: string): Promise<SaveMethodSession> =>
    apiFetch<SaveMethodSession>(`${BASE}/sessions`, {
      method: HttpMethod.Post,
      body: { gatewayModuleId, consentAccepted, consentTextVersion, returnUrl }
    })

  /**
   * Finishes a save session after the payer entered the method.
   *
   * @param gatewayModuleId - Gateway module the session was opened at.
   * @param sessionId - The session id.
   * @returns The stored method.
   * @throws Whatever `apiFetch` throws.
   */
  const completeSave = (gatewayModuleId: string, sessionId: string): Promise<StoredMethod> =>
    apiFetch<StoredMethod>(`${BASE}/sessions/${encodeURIComponent(sessionId)}/complete`, {
      method: HttpMethod.Post,
      body: { gatewayModuleId }
    })

  /**
   * Revokes a stored charge consent; the method is never charged again.
   *
   * @param consentId - The consent id.
   * @returns Whatever the endpoint answers with.
   * @throws Whatever `apiFetch` throws.
   */
  const revokeConsent = (consentId: number): Promise<unknown> =>
    apiFetch(`${BASE}/consents/${consentId}`, { method: HttpMethod.Delete })

  /**
   * Makes a stored method the one charged by default.
   *
   * @param consentId - The consent id.
   * @returns Whatever the endpoint answers with.
   * @throws Whatever `apiFetch` throws.
   */
  const setDefault = (consentId: number): Promise<unknown> =>
    apiFetch(`${BASE}/consents/${consentId}/default`, { method: HttpMethod.Post })

  return { fetchStoredMethods, beginSave, completeSave, revokeConsent, setDefault }
}
