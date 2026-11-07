/**
 * Payment methods stored for later charges: starting a save, completing it on return from the
 * gateway's page, and revoking the consent. The only caller of the stored-method endpoints in
 * `useStoredMethodsApi`.
 *
 * {@link methods} is the list the API holds, re-read after every save, revoke and default change.
 * Every refusal is kept as the API's own sentence.
 *
 * @module stores/storedmethods
 */
import { defineStore } from 'pinia'
import { useStoredMethodsApi } from '~/composables/apis/useStoredMethodsApi'
import { apiErrorMessage } from '~/lib/errorHandler'
import { ActionOutcome } from '~/types/actionoutcome'
import { SaveMethodMode } from '~/types/savemethodmode'
import type { ConsentTextVersion } from '~/types/consenttextversion'
import type { StoredMethod } from '~/types/storedmethod'
import { isSafeRedirectUrl } from '~/utils/externalRedirect'
import {
  clearPendingStoredSession,
  readPendingStoredSession,
  writePendingStoredSession
} from '~/utils/pendingStoredSession'
import { translate } from '~/utils/translate'

/** The stored-method store. */
export const useStoredMethodsStore = defineStore('storedMethods', () => {
  /** The stored-method endpoints. */
  const api = useStoredMethodsApi()

  /** The client's stored methods, as the API lists them. */
  const methods = ref<StoredMethod[]>([])
  /** Whether a save session is being opened. */
  const starting = ref(false)
  /** Whether a save session is being completed. */
  const completing = ref(false)
  /** The consent whose revoke is in flight. */
  const revokingId = ref<number | null>(null)
  /** The method whose default change is in flight. */
  const settingDefaultId = ref<number | null>(null)
  /** Whether the list is being read. */
  const loading = ref(false)
  /** The sentence for a failed list read; empty when it worked. */
  const loadError = ref('')
  /** The sentence for the last refused action; empty when there is none. */
  const error = ref('')

  /**
   * Reads the stored methods. A failure is kept in {@link loadError} and the current list stays.
   *
   * @returns Resolves when settled.
   */
  const load = async (): Promise<void> => {
    loading.value = true
    loadError.value = ''
    try {
      methods.value = await api.fetchStoredMethods()
    } catch (e: unknown) {
      loadError.value = apiErrorMessage(e)
    } finally {
      loading.value = false
    }
  }

  /**
   * Opens a save session and remembers it for the return.
   *
   * @param gatewayModuleId - Gateway module to store the method at.
   * @param consentAccepted - Whether the consent box was ticked.
   * @param consentTextVersion - Which wording the payer ticked.
   * @param returnUrl - Absolute URL the gateway returns the payer to.
   * @returns The gateway address to navigate to, or null when the attempt failed
   * ({@link error} holds the sentence).
   */
  const beginSave = async (gatewayModuleId: string, consentAccepted: boolean, consentTextVersion: ConsentTextVersion, returnUrl: string): Promise<string | null> => {
    error.value = ''
    starting.value = true
    try {
      const session = await api.beginSave(gatewayModuleId, consentAccepted, consentTextVersion, returnUrl)
      if (session.mode !== SaveMethodMode.Redirect || !session.redirectUrl || !isSafeRedirectUrl(session.redirectUrl)) {
        // The portal hosts no in-page card form, and an unsafe address must not be followed.
        error.value = translate('client.storedMethods.unsupportedMode')
        return null
      }
      writePendingStoredSession({ gatewayModuleId, sessionId: session.sessionId })
      return session.redirectUrl
    } catch (e: unknown) {
      error.value = apiErrorMessage(e)
      return null
    } finally {
      starting.value = false
    }
  }

  /**
   * Completes the session the payer left for, when there is one.
   *
   * @returns Null when no session was pending; otherwise how it ended.
   */
  const completePending = async (): Promise<ActionOutcome | null> => {
    const pending = readPendingStoredSession()
    if (pending === null) {
      return null
    }
    error.value = ''
    completing.value = true
    try {
      await api.completeSave(pending.gatewayModuleId, pending.sessionId)
      clearPendingStoredSession()
      await load()
      return ActionOutcome.Succeeded
    } catch (e: unknown) {
      error.value = apiErrorMessage(e)
      // A refused session cannot succeed on a retry; keeping it would repeat the refusal on every return.
      clearPendingStoredSession()
      await load()
      return ActionOutcome.Failed
    } finally {
      completing.value = false
    }
  }

  /**
   * Revokes a stored method's consent and drops it from the list once the server confirmed.
   *
   * @param consentId - The consent id.
   * @returns How it ended; on `Failed`, {@link error} holds the sentence.
   */
  const revoke = async (consentId: number): Promise<ActionOutcome> => {
    error.value = ''
    revokingId.value = consentId
    try {
      await api.revokeConsent(consentId)
      await load()
      return ActionOutcome.Succeeded
    } catch (e: unknown) {
      error.value = apiErrorMessage(e)
      return ActionOutcome.Failed
    } finally {
      revokingId.value = null
    }
  }

  /**
   * Makes a stored method the default and re-reads the list.
   *
   * @param consentId - The consent id.
   * @returns How it ended; on `Failed`, {@link error} holds the sentence.
   */
  const makeDefault = async (consentId: number): Promise<ActionOutcome> => {
    error.value = ''
    settingDefaultId.value = consentId
    try {
      await api.setDefault(consentId)
      await load()
      return ActionOutcome.Succeeded
    } catch (e: unknown) {
      error.value = apiErrorMessage(e)
      return ActionOutcome.Failed
    } finally {
      settingDefaultId.value = null
    }
  }

  return {
    methods, starting, completing, revokingId, settingDefaultId, loading, loadError, error,
    load, beginSave, completePending, revoke, makeDefault
  }
})
