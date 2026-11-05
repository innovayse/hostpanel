/**
 * The follow-up to a checkout paid at a hosted gateway: when the payer ticked "save this payment
 * method for automatic renewals", start a stored-method session once the payment is confirmed.
 *
 * It never starts on a failed or pending payment (the caller only asks after a confirmed `Paid`),
 * and the choice is spent when it is read, so a reload cannot start a second session. It is acted on
 * only by the account that made it: another account on the same browser starts nothing. A session that
 * cannot be started, whether the gateway does not support stored credentials
 * (`STORED_CREDENTIALS_NOT_SUPPORTED`) or anything else fails, leaves the paid order untouched and
 * only raises {@link failed}, which the page shows as a non-blocking notice.
 *
 * @module composables/useCheckoutRenewalSave
 */

import { useStoredMethodStart } from '~/composables/useStoredMethodStart'
import { useAuthStore } from '~/stores/auth'
import { ConsentTextVersion } from '~/types/consenttextversion'
import { consumeCheckoutSave, forgetCheckoutSave, hasCheckoutSave } from '~/utils/pendingCheckoutSave'

/**
 * The renewal-save follow-up.
 *
 * @returns Whether a session is being opened, whether the attempt failed, and the two actions.
 */
export const useCheckoutRenewalSave = () => {
  /** Starts a stored-method session and follows its redirect. */
  const { start } = useStoredMethodStart()

  /** The signed-in account, whose choice alone may be acted on. */
  const authStore = useAuthStore()

  /** Whether a session is being opened, or the browser is on its way to the gateway page. */
  const saving = ref(false)

  /** Whether the chosen save could not be started; the payment itself is unaffected. */
  const failed = ref(false)

  /**
   * Starts the session the payer asked for at checkout, if they asked, for an order confirmed as paid.
   *
   * @param orderId - The paid order.
   * @returns Resolves when the attempt has settled or the browser has been handed on.
   */
  const startIfChosen = async (orderId: string | number): Promise<void> => {
    if (!hasCheckoutSave(orderId)) {
      return
    }
    // After the trip to the bank the store may not know the account yet; an account that cannot be
    // identified is treated as another one and the entry is dropped unused.
    try {
      await authStore.fetchUser()
    } catch {
      // Unknown account: consumeCheckoutSave below refuses.
    }
    const gatewayModuleId = consumeCheckoutSave(orderId, authStore.user?.email)
    if (gatewayModuleId === null) {
      return
    }
    saving.value = true
    failed.value = false
    try {
      // Ticking the box beside the agreement wording is the consent to the checkout agreement wording; the server records it.
      if (!(await start(gatewayModuleId, true, ConsentTextVersion.CheckoutAgreement))) {
        failed.value = true
        saving.value = false
      }
    } catch {
      failed.value = true
      saving.value = false
    }
  }

  /**
   * Drops the choice for an order whose payment did not go through.
   *
   * @param orderId - The order that was declined.
   */
  const discard = (orderId: string | number): void => {
    forgetCheckoutSave(orderId)
  }

  return { saving, failed, startIfChosen, discard }
}
