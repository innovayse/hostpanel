/**
 * Unit tests for StoredMethodsCard: the list, the consent gate, completing on return, making default and revoking.
 * The card, the stored-methods store, its API composable and the transport run for real against a stub BFF that
 * keeps the client's stored methods, so a change is visible in the next read as it is in production.
 */
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { flushPromises } from '@vue/test-utils'
import { mountAccount } from '~/tests/helpers/mountAccount'
import type { MountWithKitOptions } from '~/tests/helpers/mountWithKit'
import { AccountEndpoint, completeSessionPath, consentPath, defaultConsentPath } from '~/tests/helpers/accountPaths'
import { bodyOf } from '~/tests/helpers/requestBody'
import { stubLocalePath, stubRoute } from '~/tests/helpers/runtimeStubs'
import { requestsTo, routeTable, refusal } from '~/tests/helpers/stubRoutes'
import type { StubRouteTable } from '~/tests/helpers/stubRoutes'
import type { StubServer } from '~/tests/helpers/stubServer'
import { startTransport } from '~/tests/helpers/stubTransport'
import StoredMethodsCard from '~/components/account/StoredMethodsCard.vue'
import { useAccountStore } from '~/stores/account'
import { useStoredMethodsStore } from '~/stores/storedmethods'
import type { GatewayMethod } from '~/types/gatewaymethod'
import { HttpMethod } from '~/types/httpmethod'
import { PaymentModule } from '~/types/paymentmodule'
import type { StoredMethod } from '~/types/storedmethod'
import { readPendingStoredSession, writePendingStoredSession } from '~/utils/pendingStoredSession'

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

/** The built-in transfer, which no method can be stored at. */
const transfer: GatewayMethod = { module: PaymentModule.BankTransfer, displayName: 'Bank transfer' }

/** Two gateways, one of them the built-in transfer. */
const gateways: GatewayMethod[] = [transfer, { module: 'card_gateway', displayName: 'Card gateway' }]

/** The stub playing the BFF. */
let stub: StubServer

/** The replies the stub gives, by `METHOD /path`; each test adds or replaces the ones it needs. */
let table: StubRouteTable

/** The stored methods the BFF holds; a default change or a revoke edits them. */
let held: StoredMethod[]

beforeEach(async () => {
  held = []
  table = { [`${HttpMethod.Get} ${AccountEndpoint.StoredMethods}`]: () => ({ body: held }) }
  stub = await startTransport(routeTable(table))
  stubLocalePath('/en')
  stubRoute()
})

afterEach(async () => {
  vi.unstubAllGlobals()
  window.sessionStorage.clear()
  await stub.close()
})

/**
 * Mounts the card with the given gateways loaded and lets everything it does on mount settle.
 *
 * @param options - Mount options as for `mountAccount`.
 * @param offered - The gateways the account store holds.
 * @returns The wrapper.
 */
const mountCard = async (options: MountWithKitOptions = {}, offered: GatewayMethod[] = gateways) => {
  const wrapper = mountAccount(StoredMethodsCard, options)
  useAccountStore().gatewayMethods = offered
  // The first turn decides whether a pending session is completed or the list is read; the store's flags then say when it is done.
  await flushPromises()
  const store = useStoredMethodsStore()
  await vi.waitFor(() => expect(store.loading || store.completing).toBe(false))
  await flushPromises()
  return wrapper
}

/**
 * The reads of the stored-method list the stub received.
 *
 * @returns The list requests, oldest first.
 */
const listReads = () => requestsTo(stub.requests, AccountEndpoint.StoredMethods)

describe('StoredMethodsCard', () => {
  it('reads the list on mount and shows the empty line when there is none', async () => {
    const wrapper = await mountCard()

    expect(listReads()).toHaveLength(1)
    expect(wrapper.find('[data-testid="stored-methods-empty"]').exists()).toBe(true)
  })

  it('shows the sentence when the list cannot be read', async () => {
    table[`${HttpMethod.Get} ${AccountEndpoint.StoredMethods}`] = refusal('Unreachable.')

    const wrapper = await mountCard()

    expect(wrapper.find('[data-testid="stored-methods-load-error"]').text()).toBe('Unreachable.')
    expect(wrapper.find('[data-testid="stored-methods-empty"]').exists()).toBe(false)
  })

  it('keeps the start button disabled until a gateway is chosen and consent is given', async () => {
    const wrapper = await mountCard()

    expect(wrapper.find('[data-testid="stored-methods-start"]').attributes('disabled')).toBeDefined()
  })

  it('lists a method with its brand, last digits and expiry', async () => {
    held = [stored]

    const wrapper = await mountCard()

    const row = wrapper.find('[data-testid="stored-method-5"]')
    expect(row.text()).toContain('visa')
    expect(row.text()).toContain('4242')
    expect(row.text()).toContain('09/2029')
    expect(wrapper.find('[data-testid="stored-method-default-badge-5"]').exists()).toBe(false)
    expect(wrapper.find('[data-testid="stored-method-default-5"]').exists()).toBe(true)
  })

  it('shows the unavailable line and the default badge, without a make-default action, for the default method', async () => {
    held = [unavailable]

    const wrapper = await mountCard()

    expect(wrapper.find('[data-testid="stored-method-unavailable-6"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="stored-method-default-badge-6"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="stored-method-default-6"]').exists()).toBe(false)
  })

  it('makes a method the default and reads the list again', async () => {
    held = [stored]
    table[`${HttpMethod.Post} ${defaultConsentPath(5)}`] = () => {
      held = held.map(method => ({ ...method, isDefault: true }))
      return { body: {} }
    }
    const wrapper = await mountCard()

    await wrapper.find('[data-testid="stored-method-default-5"]').trigger('click')

    await vi.waitFor(() => expect(wrapper.find('[data-testid="stored-methods-success"]').exists()).toBe(true))
    const changes = requestsTo(stub.requests, defaultConsentPath(5))
    expect(changes).toHaveLength(1)
    expect(changes[0]?.method).toBe(HttpMethod.Post)
    expect(listReads()).toHaveLength(2)
    expect(wrapper.find('[data-testid="stored-method-default-badge-5"]').exists()).toBe(true)
  })

  it('asks for confirmation before revoking, then revokes and reads the list again', async () => {
    held = [stored]
    table[`${HttpMethod.Delete} ${consentPath(5)}`] = () => {
      held = []
      return { body: {} }
    }
    const wrapper = await mountCard()

    await wrapper.find('[data-testid="stored-method-revoke-5"]').trigger('click')
    expect(requestsTo(stub.requests, consentPath(5))).toHaveLength(0)

    await wrapper.find('[data-testid="ui-confirm-confirm"]').trigger('click')

    await vi.waitFor(() => expect(wrapper.find('[data-testid="stored-methods-success"]').exists()).toBe(true))
    const revocations = requestsTo(stub.requests, consentPath(5))
    expect(revocations).toHaveLength(1)
    expect(revocations[0]?.method).toBe(HttpMethod.Delete)
    expect(wrapper.find('[data-testid="stored-method-5"]').exists()).toBe(false)
  })

  it('shows the API sentence when a revoke is refused', async () => {
    held = [stored]
    table[`${HttpMethod.Delete} ${consentPath(5)}`] = refusal('Refused.')
    const wrapper = await mountCard()

    await wrapper.find('[data-testid="stored-method-revoke-5"]').trigger('click')
    await wrapper.find('[data-testid="ui-confirm-confirm"]').trigger('click')

    await vi.waitFor(() => expect(wrapper.find('[data-testid="stored-methods-error"]').text()).toBe('Refused.'))
    expect(wrapper.find('[data-testid="stored-method-5"]').exists()).toBe(true)
  })

  it('completes the pending session when the payer returns and shows the refreshed list', async () => {
    writePendingStoredSession({ gatewayModuleId: 'card_gateway', sessionId: 's1' })
    held = [stored]
    table[`${HttpMethod.Post} ${completeSessionPath('s1')}`] = { body: stored }
    stubRoute({ query: { tab: 'payment', storedReturn: '1' } })

    const wrapper = await mountCard()

    const completions = requestsTo(stub.requests, completeSessionPath('s1'))
    expect(completions).toHaveLength(1)
    expect(completions[0]?.method).toBe(HttpMethod.Post)
    expect(bodyOf(completions[0])).toEqual({ gatewayModuleId: 'card_gateway' })
    expect(wrapper.find('[data-testid="stored-method-5"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="stored-methods-success"]').exists()).toBe(true)
  })

  it('reads the list on return when no session is pending', async () => {
    stubRoute({ query: { tab: 'payment', storedReturn: '1' } })

    const wrapper = await mountCard()

    expect(stub.requests.filter(request => request.method === HttpMethod.Post)).toHaveLength(0)
    expect(listReads()).toHaveLength(1)
    expect(wrapper.find('[data-testid="stored-methods-empty"]').exists()).toBe(true)
  })

  it('forgets the pending session when completing it is refused', async () => {
    writePendingStoredSession({ gatewayModuleId: 'card_gateway', sessionId: 's1' })
    table[`${HttpMethod.Post} ${completeSessionPath('s1')}`] = refusal('Expired.')
    stubRoute({ query: { tab: 'payment', storedReturn: '1' } })

    const wrapper = await mountCard()

    expect(wrapper.find('[data-testid="stored-methods-error"]').text()).toBe('Expired.')
    expect(readPendingStoredSession()).toBeNull()
  })

  it('labels the row buttons with the brand and last digits', async () => {
    held = [stored]

    // The default `$t` echoes the key only; this one also echoes the method parameter the label carries.
    const wrapper = await mountCard({
      mocks: { $t: (key: string, params?: { method?: string }) => `${key} ${params?.method ?? ''}` }
    })

    expect(wrapper.find('[data-testid="stored-method-default-5"]').attributes('aria-label')).toContain('visa 4242')
    expect(wrapper.find('[data-testid="stored-method-revoke-5"]').attributes('aria-label')).toContain('visa 4242')
  })

  it('explains when no gateway can store a method instead of showing an empty select', async () => {
    const wrapper = await mountCard({}, [transfer])

    expect(wrapper.find('[data-testid="stored-methods-no-gateways"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="stored-methods-gateway"]').exists()).toBe(false)
    expect(wrapper.find('[data-testid="stored-methods-start"]').attributes('disabled')).toBeDefined()
  })

  it('does not complete anything without the return marker', async () => {
    writePendingStoredSession({ gatewayModuleId: 'card_gateway', sessionId: 's1' })

    await mountCard()

    expect(requestsTo(stub.requests, completeSessionPath('s1'))).toHaveLength(0)
  })
})
