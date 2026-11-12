/**
 * Unit tests for StoredMethodsCard: the list, the consent gate, completing on return, making default and revoking.
 */
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { flushPromises } from '@vue/test-utils'
import { mountAccount } from '~/tests/helpers/mountAccount'
import type { MountWithKitOptions } from '~/tests/helpers/mountWithKit'
import { stubLocalePath, stubRoute } from '~/tests/helpers/runtimeStubs'
import StoredMethodsCard from '~/components/account/StoredMethodsCard.vue'
import { useAccountStore } from '~/stores/account'
import { PaymentModule } from '~/types/paymentmodule'
import type { StoredMethod } from '~/types/storedmethod'
import { readPendingStoredSession, writePendingStoredSession } from '~/utils/pendingStoredSession'

/** The stored-method API the store calls; a test sets what each call answers. */
const api = vi.hoisted(() => ({
  fetchStoredMethods: vi.fn(),
  beginSave: vi.fn(),
  completeSave: vi.fn(),
  revokeConsent: vi.fn(),
  setDefault: vi.fn()
}))

vi.mock('~/composables/apis/useStoredMethodsApi', () => ({ useStoredMethodsApi: () => api }))
vi.mock('~/composables/apis/useClientApi', () => ({ useClientApi: () => ({}) }))
vi.mock('~/composables/apis/useBillingApi', () => ({ useBillingApi: () => ({}) }))
vi.mock('~/composables/apis/useAuthApi', () => ({ useAuthApi: () => ({}) }))
vi.mock('qrcode', () => ({ default: { toDataURL: async (): Promise<string> => '' } }))

/** One stored card, not the default. */
const stored: StoredMethod = {
  consentId: 5, gatewayModuleId: 'card_gateway', detailsAvailable: true, brand: 'visa', last4: '4242',
  expiryMonth: 9, expiryYear: 2029, isDefault: false
}

/** A stored method whose details the gateway could not report, and which is the default. */
const unavailable: StoredMethod = {
  consentId: 6, gatewayModuleId: 'card_gateway', detailsAvailable: false, brand: null, last4: null,
  expiryMonth: 0, expiryYear: 0, isDefault: true
}

beforeEach(() => {
  Object.values(api).forEach(mock => mock.mockReset())
  api.fetchStoredMethods.mockResolvedValue([])
  stubLocalePath('/en')
  stubRoute()
})

afterEach(() => {
  vi.unstubAllGlobals()
  window.sessionStorage.clear()
})

/**
 * Mounts the card with two gateways loaded, one of them the built-in transfer, and lets the first read settle.
 *
 * @param options - Mount options as for `mountAccount`.
 * @returns The wrapper.
 */
const mountCard = async (options: MountWithKitOptions = {}) => {
  const wrapper = mountAccount(StoredMethodsCard, options)
  useAccountStore().gatewayMethods = [
    { module: PaymentModule.BankTransfer, displayName: 'Bank transfer', supportsStoredCredentials: false },
    { module: 'card_gateway', displayName: 'Card gateway', supportsStoredCredentials: false }
  ]
  await flushPromises()
  return wrapper
}

describe('StoredMethodsCard', () => {
  it('reads the list on mount and shows the empty line when there is none', async () => {
    const wrapper = await mountCard()

    expect(api.fetchStoredMethods).toHaveBeenCalledTimes(1)
    expect(wrapper.find('[data-testid="stored-methods-empty"]').exists()).toBe(true)
  })

  it('shows the sentence when the list cannot be read', async () => {
    api.fetchStoredMethods.mockRejectedValue({ data: { detail: 'Unreachable.' } })

    const wrapper = await mountCard()

    expect(wrapper.find('[data-testid="stored-methods-load-error"]').text()).toBe('Unreachable.')
    expect(wrapper.find('[data-testid="stored-methods-empty"]').exists()).toBe(false)
  })

  it('keeps the start button disabled until a gateway is chosen and consent is given', async () => {
    const wrapper = await mountCard()

    expect(wrapper.find('[data-testid="stored-methods-start"]').attributes('disabled')).toBeDefined()
  })

  it('lists a method with its brand, last digits and expiry', async () => {
    api.fetchStoredMethods.mockResolvedValue([stored])

    const wrapper = await mountCard()

    const row = wrapper.find('[data-testid="stored-method-5"]')
    expect(row.text()).toContain('visa')
    expect(row.text()).toContain('4242')
    expect(row.text()).toContain('09/2029')
    expect(wrapper.find('[data-testid="stored-method-default-badge-5"]').exists()).toBe(false)
    expect(wrapper.find('[data-testid="stored-method-default-5"]').exists()).toBe(true)
  })

  it('shows the unavailable line and the default badge, without a make-default action, for the default method', async () => {
    api.fetchStoredMethods.mockResolvedValue([unavailable])

    const wrapper = await mountCard()

    expect(wrapper.find('[data-testid="stored-method-unavailable-6"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="stored-method-default-badge-6"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="stored-method-default-6"]').exists()).toBe(false)
  })

  it('makes a method the default and reads the list again', async () => {
    api.fetchStoredMethods.mockResolvedValueOnce([stored]).mockResolvedValueOnce([{ ...stored, isDefault: true }])
    api.setDefault.mockResolvedValue(undefined)
    const wrapper = await mountCard()

    await wrapper.find('[data-testid="stored-method-default-5"]').trigger('click')
    await flushPromises()

    expect(api.setDefault).toHaveBeenCalledWith(5)
    expect(api.fetchStoredMethods).toHaveBeenCalledTimes(2)
    expect(wrapper.find('[data-testid="stored-method-default-badge-5"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="stored-methods-success"]').exists()).toBe(true)
  })

  it('asks for confirmation before revoking, then revokes and reads the list again', async () => {
    api.fetchStoredMethods.mockResolvedValueOnce([stored]).mockResolvedValueOnce([])
    api.revokeConsent.mockResolvedValue(undefined)
    const wrapper = await mountCard()

    await wrapper.find('[data-testid="stored-method-revoke-5"]').trigger('click')
    expect(api.revokeConsent).not.toHaveBeenCalled()

    await wrapper.find('[data-testid="ui-confirm-confirm"]').trigger('click')
    await flushPromises()

    expect(api.revokeConsent).toHaveBeenCalledWith(5)
    expect(wrapper.find('[data-testid="stored-method-5"]').exists()).toBe(false)
    expect(wrapper.find('[data-testid="stored-methods-success"]').exists()).toBe(true)
  })

  it('shows the API sentence when a revoke is refused', async () => {
    api.fetchStoredMethods.mockResolvedValue([stored])
    api.revokeConsent.mockRejectedValue({ data: { detail: 'Refused.' } })
    const wrapper = await mountCard()

    await wrapper.find('[data-testid="stored-method-revoke-5"]').trigger('click')
    await wrapper.find('[data-testid="ui-confirm-confirm"]').trigger('click')
    await flushPromises()

    expect(wrapper.find('[data-testid="stored-methods-error"]').text()).toBe('Refused.')
    expect(wrapper.find('[data-testid="stored-method-5"]').exists()).toBe(true)
  })

  it('completes the pending session when the payer returns and shows the refreshed list', async () => {
    writePendingStoredSession({ gatewayModuleId: 'card_gateway', sessionId: 's1' })
    api.completeSave.mockResolvedValue(stored)
    api.fetchStoredMethods.mockResolvedValue([stored])
    stubRoute({ query: { tab: 'payment', storedReturn: '1' } })

    const wrapper = await mountCard()

    expect(api.completeSave).toHaveBeenCalledWith('card_gateway', 's1')
    expect(wrapper.find('[data-testid="stored-method-5"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="stored-methods-success"]').exists()).toBe(true)
  })

  it('reads the list on return when no session is pending', async () => {
    stubRoute({ query: { tab: 'payment', storedReturn: '1' } })

    const wrapper = await mountCard()

    expect(api.completeSave).not.toHaveBeenCalled()
    expect(api.fetchStoredMethods).toHaveBeenCalledTimes(1)
    expect(wrapper.find('[data-testid="stored-methods-empty"]').exists()).toBe(true)
  })

  it('forgets the pending session when completing it is refused', async () => {
    writePendingStoredSession({ gatewayModuleId: 'card_gateway', sessionId: 's1' })
    api.completeSave.mockRejectedValue({ data: { detail: 'Expired.' } })
    stubRoute({ query: { tab: 'payment', storedReturn: '1' } })

    const wrapper = await mountCard()

    expect(wrapper.find('[data-testid="stored-methods-error"]').text()).toBe('Expired.')
    expect(readPendingStoredSession()).toBeNull()
  })

  it('labels the row buttons with the brand and last digits', async () => {
    api.fetchStoredMethods.mockResolvedValue([stored])

    // The default `$t` echoes the key only; this one also echoes the method parameter the label carries.
    const wrapper = await mountCard({
      mocks: { $t: (key: string, params?: { method?: string }) => `${key} ${params?.method ?? ''}` }
    })

    expect(wrapper.find('[data-testid="stored-method-default-5"]').attributes('aria-label')).toContain('visa 4242')
    expect(wrapper.find('[data-testid="stored-method-revoke-5"]').attributes('aria-label')).toContain('visa 4242')
  })

  it('explains when no gateway can store a method instead of showing an empty select', async () => {
    const wrapper = mountAccount(StoredMethodsCard)
    useAccountStore().gatewayMethods = [{ module: PaymentModule.BankTransfer, displayName: 'Bank transfer', supportsStoredCredentials: false }]
    await flushPromises()

    expect(wrapper.find('[data-testid="stored-methods-no-gateways"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="stored-methods-gateway"]').exists()).toBe(false)
    expect(wrapper.find('[data-testid="stored-methods-start"]').attributes('disabled')).toBeDefined()
  })

  it('does not complete anything without the return marker', async () => {
    writePendingStoredSession({ gatewayModuleId: 'card_gateway', sessionId: 's1' })

    await mountCard()

    expect(api.completeSave).not.toHaveBeenCalled()
  })
})
