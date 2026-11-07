/**
 * Starts a stored-method save session and hands the browser to the gateway page.
 *
 * The one place that builds the return address (the account's payment tab with the return marker,
 * where `StoredMethodsCard` completes the session) and follows the redirect. The saved-methods card
 * and the post-checkout save both start a session through it, so the flow is written once. Consent
 * recording stays on the server; the session call carries the `consentAccepted` flag the caller got
 * from the payer and the {@link ConsentTextVersion} of the wording they ticked, which the API requires.
 *
 * @module composables/useStoredMethodStart
 */

import { clientAccountTabPath } from '~/constants/clientRoutes'
import { STORED_METHOD_RETURN_QUERY } from '~/constants/storedMethodReturn'
import { useStoredMethodsStore } from '~/stores/storedmethods'
import { AccountTab } from '~/types/accounttab'
import type { ConsentTextVersion } from '~/types/consenttextversion'
import { redirectToExternal } from '~/utils/externalRedirect'

/**
 * The session starter.
 *
 * @returns The `start` action.
 */
export const useStoredMethodStart = () => {
  /** Prefixes a path with the active locale. */
  const localePath = useLocalePath()

  /** The stored-method store, the only caller of the stored-method endpoints. */
  const storedStore = useStoredMethodsStore()

  /**
   * Opens a save session at a gateway and, when it answers, sends the browser to the gateway page.
   *
   * @param gatewayModuleId - Gateway module to store the method at.
   * @param consentAccepted - Whether the payer agreed to later charges.
   * @param consentTextVersion - Which wording the payer ticked (account card or checkout agreement).
   * @returns True when the browser was handed on; false when the attempt failed (the store holds the sentence).
   */
  const start = async (
    gatewayModuleId: string,
    consentAccepted: boolean,
    consentTextVersion: ConsentTextVersion
  ): Promise<boolean> => {
    const returnUrl = new URL(
      localePath(`${clientAccountTabPath(AccountTab.Payment)}&${STORED_METHOD_RETURN_QUERY}=1`),
      window.location.origin
    ).toString()
    const redirectUrl = await storedStore.beginSave(gatewayModuleId, consentAccepted, consentTextVersion, returnUrl)
    if (!redirectUrl) {
      return false
    }
    redirectToExternal(redirectUrl)
    return true
  }

  return { start }
}
