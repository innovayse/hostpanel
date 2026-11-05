/**
 * Tests for `composables/useStoredMethodStart.ts`: the shared start of a stored-method save
 * session, used by the saved-methods card and by the post-checkout save.
 *
 * @module tests/composables/useStoredMethodStart.test
 */

import { beforeEach, describe, expect, it, vi } from 'vitest'
import { createPinia, setActivePinia } from 'pinia'

/** The stored-method API the store calls; a test sets what each call answers. */
const api = vi.hoisted(() => ({
  fetchStoredMethods: vi.fn(),
  beginSave: vi.fn(),
  completeSave: vi.fn(),
  revokeConsent: vi.fn(),
  setDefault: vi.fn()
}))

/** The redirect the composable follows once a session is open. */
const redirect = vi.hoisted(() => vi.fn())

vi.mock('~/composables/apis/useStoredMethodsApi', () => ({
  /**
   * Stands in for the stored-method API composable.
   *
   * @returns The shared API mocks.
   */
  useStoredMethodsApi: () => api
}))
vi.mock('~/utils/externalRedirect', () => ({
  /**
   * Stands in for the address check: only https addresses are safe.
   *
   * @param target - The address to judge.
   * @returns True for an https address.
   */
  isSafeRedirectUrl: (target: string): boolean => target.startsWith('https://'),
  redirectToExternal: redirect
}))

const { ConsentTextVersion } = await import('~/types/consenttextversion')
const { useStoredMethodStart } = await import('~/composables/useStoredMethodStart')
const { readPendingStoredSession } = await import('~/utils/pendingStoredSession')

beforeEach(() => {
  setActivePinia(createPinia())
  Object.values(api).forEach(mock => mock.mockReset())
  redirect.mockReset()
  window.sessionStorage.clear()
  vi.stubGlobal('useLocalePath', () => (path: string) => `/en${path}`)
})

describe('useStoredMethodStart', () => {
  it('opens a session with the account payment tab as the return address and follows the redirect', async () => {
    api.beginSave.mockResolvedValue({ sessionId: 's1', mode: 'Redirect', redirectUrl: 'https://gateway.example/save' })
    const { start } = useStoredMethodStart()

    const handedOn = await start('card_gateway', true, ConsentTextVersion.AccountCard)

    expect(handedOn).toBe(true)
    const [module, consent, version, returnUrl] = api.beginSave.mock.calls[0] as [string, boolean, string, string]
    expect(module).toBe('card_gateway')
    expect(consent).toBe(true)
    expect(version).toBe(ConsentTextVersion.AccountCard)
    expect(new URL(returnUrl).pathname).toBe('/en/client/account')
    expect(new URL(returnUrl).search).toBe('?tab=payment&storedReturn=1')
    expect(redirect).toHaveBeenCalledWith('https://gateway.example/save')
    expect(readPendingStoredSession()).toEqual({ gatewayModuleId: 'card_gateway', sessionId: 's1' })
  })

  it('does not redirect, and says so, when the session is refused', async () => {
    api.beginSave.mockRejectedValue({ data: { type: 'STORED_CREDENTIALS_NOT_SUPPORTED', detail: 'Not supported.' } })
    const { start } = useStoredMethodStart()

    expect(await start('card_gateway', true, ConsentTextVersion.CheckoutAgreement)).toBe(false)

    expect(redirect).not.toHaveBeenCalled()
    expect(readPendingStoredSession()).toBeNull()
  })

  it('does not redirect for a session whose address is not safe', async () => {
    api.beginSave.mockResolvedValue({ sessionId: 's1', mode: 'Redirect', redirectUrl: 'javascript:alert(1)' })
    const { start } = useStoredMethodStart()

    expect(await start('card_gateway', true, ConsentTextVersion.AccountCard)).toBe(false)

    expect(redirect).not.toHaveBeenCalled()
  })
})
