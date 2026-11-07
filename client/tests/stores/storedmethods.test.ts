/**
 * Tests for `stores/storedmethods.ts`: the sentences, flags and list the stored-method card renders from.
 *
 * @module tests/stores/storedmethods.test
 */

import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { createPinia, setActivePinia } from 'pinia'
import { useStoredMethodsStore } from '~/stores/storedmethods'
import { ActionOutcome } from '~/types/actionoutcome'
import { ConsentTextVersion } from '~/types/consenttextversion'
import { SaveMethodMode } from '~/types/savemethodmode'
import type { StoredMethod } from '~/types/storedmethod'
import { readPendingStoredSession, writePendingStoredSession } from '~/utils/pendingStoredSession'

/** Mocked stored-method endpoints. */
const api = vi.hoisted(() => ({
  fetchStoredMethods: vi.fn(),
  beginSave: vi.fn(),
  completeSave: vi.fn(),
  revokeConsent: vi.fn(),
  setDefault: vi.fn()
}))

vi.mock('~/composables/apis/useStoredMethodsApi', () => ({ useStoredMethodsApi: () => api }))

/** One stored card. */
const stored: StoredMethod = {
  consentId: 5, gatewayModuleId: 'card_gateway', detailsAvailable: true, brand: 'visa', last4: '4242',
  expiryMonth: 9, expiryYear: 2029, isDefault: false
}

/** A refusal as `apiFetch` throws it. */
const refusal = { data: { detail: 'Refused.' } }

beforeEach(() => {
  setActivePinia(createPinia())
  Object.values(api).forEach(mock => mock.mockReset())
  api.fetchStoredMethods.mockResolvedValue([])
})

afterEach(() => {
  window.sessionStorage.clear()
})

describe('stored methods store', () => {
  it('loads the list', async () => {
    api.fetchStoredMethods.mockResolvedValue([stored])
    const store = useStoredMethodsStore()

    await store.load()

    expect(store.methods).toEqual([stored])
    expect(store.loadError).toBe('')
    expect(store.loading).toBe(false)
  })

  it('keeps the current list and the sentence when the read fails', async () => {
    api.fetchStoredMethods.mockResolvedValueOnce([stored]).mockRejectedValueOnce(refusal)
    const store = useStoredMethodsStore()
    await store.load()

    await store.load()

    expect(store.loadError).toBe('Refused.')
    expect(store.methods).toEqual([stored])
  })

  it('opens a redirect session, remembers it and returns the address', async () => {
    api.beginSave.mockResolvedValue({ sessionId: 's1', mode: SaveMethodMode.Redirect, clientToken: null, redirectUrl: 'https://pay.example/s1' })
    const store = useStoredMethodsStore()

    const url = await store.beginSave('card_gateway', true, ConsentTextVersion.AccountCard, 'https://x/return')

    expect(url).toBe('https://pay.example/s1')
    expect(api.beginSave).toHaveBeenCalledWith('card_gateway', true, ConsentTextVersion.AccountCard, 'https://x/return')
    expect(readPendingStoredSession()).toEqual({ gatewayModuleId: 'card_gateway', sessionId: 's1' })
    expect(store.starting).toBe(false)
  })

  it('refuses an embedded session, which this portal cannot show', async () => {
    api.beginSave.mockResolvedValue({ sessionId: 's1', mode: SaveMethodMode.EmbeddedToken, clientToken: 'tok', redirectUrl: null })
    const store = useStoredMethodsStore()

    expect(await store.beginSave('card_gateway', true, ConsentTextVersion.AccountCard, 'https://x/return')).toBeNull()
    expect(store.error).not.toBe('')
    expect(readPendingStoredSession()).toBeNull()
  })

  it('keeps the API sentence when opening the session is refused', async () => {
    api.beginSave.mockRejectedValue(refusal)
    const store = useStoredMethodsStore()

    expect(await store.beginSave('card_gateway', false, ConsentTextVersion.AccountCard, 'https://x/return')).toBeNull()
    expect(store.error).toBe('Refused.')
    expect(store.starting).toBe(false)
  })

  it('completes nothing when no session is pending', async () => {
    const store = useStoredMethodsStore()

    expect(await store.completePending()).toBeNull()
    expect(api.completeSave).not.toHaveBeenCalled()
  })

  it('completes the pending session, re-reads the list and forgets the session', async () => {
    writePendingStoredSession({ gatewayModuleId: 'card_gateway', sessionId: 's1' })
    api.completeSave.mockResolvedValue(stored)
    api.fetchStoredMethods.mockResolvedValue([stored])
    const store = useStoredMethodsStore()

    expect(await store.completePending()).toBe(ActionOutcome.Succeeded)
    expect(api.completeSave).toHaveBeenCalledWith('card_gateway', 's1')
    expect(store.methods).toEqual([stored])
    expect(readPendingStoredSession()).toBeNull()
  })

  it('forgets the session when completing is refused, as a refused session cannot succeed on a retry', async () => {
    writePendingStoredSession({ gatewayModuleId: 'card_gateway', sessionId: 's1' })
    api.completeSave.mockRejectedValue(refusal)
    const store = useStoredMethodsStore()

    expect(await store.completePending()).toBe(ActionOutcome.Failed)
    expect(store.error).toBe('Refused.')
    expect(readPendingStoredSession()).toBeNull()
    expect(store.completing).toBe(false)
  })

  it('re-reads the list after a revoke', async () => {
    api.revokeConsent.mockResolvedValue(undefined)
    const store = useStoredMethodsStore()

    expect(await store.revoke(5)).toBe(ActionOutcome.Succeeded)
    expect(api.revokeConsent).toHaveBeenCalledWith(5)
    expect(api.fetchStoredMethods).toHaveBeenCalledTimes(1)
    expect(store.revokingId).toBeNull()
  })

  it('keeps the sentence and does not re-read when the revoke is refused', async () => {
    api.revokeConsent.mockRejectedValue(refusal)
    const store = useStoredMethodsStore()

    expect(await store.revoke(5)).toBe(ActionOutcome.Failed)
    expect(store.error).toBe('Refused.')
    expect(api.fetchStoredMethods).not.toHaveBeenCalled()
  })

  it('makes a method the default and re-reads the list', async () => {
    api.setDefault.mockResolvedValue(undefined)
    api.fetchStoredMethods.mockResolvedValue([{ ...stored, isDefault: true }])
    const store = useStoredMethodsStore()

    expect(await store.makeDefault(5)).toBe(ActionOutcome.Succeeded)
    expect(api.setDefault).toHaveBeenCalledWith(5)
    expect(store.methods[0]?.isDefault).toBe(true)
    expect(store.settingDefaultId).toBeNull()
  })

  it('keeps the sentence when the default change is refused', async () => {
    api.setDefault.mockRejectedValue(refusal)
    const store = useStoredMethodsStore()

    expect(await store.makeDefault(5)).toBe(ActionOutcome.Failed)
    expect(store.error).toBe('Refused.')
    expect(store.settingDefaultId).toBeNull()
  })
})
