/**
 * Unit tests for TwoFactorCard: state wording, enrolment start and the disable flow. The account
 * store, the auth API composable and the transport run for real against a stub BFF; only the QR
 * renderer (a third-party package) is faked.
 */
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { flushPromises } from '@vue/test-utils'
import { mountAccount } from '~/tests/helpers/mountAccount'
import TwoFactorCard from '~/components/account/TwoFactorCard.vue'
import { AccountEndpoint } from '~/tests/helpers/accountPaths'
import { requestsTo, routeTable, refusal } from '~/tests/helpers/stubRoutes'
import type { StubRouteTable } from '~/tests/helpers/stubRoutes'
import type { StubServer } from '~/tests/helpers/stubServer'
import { startTransport } from '~/tests/helpers/stubTransport'
import { HttpMethod } from '~/types/httpmethod'
import { waitUntil } from '~/tests/helpers/waitUntil'

vi.mock('qrcode', () => ({
  default: {
    /**
     * Stands in for the QR renderer.
     *
     * @returns A fixed data URL.
     */
    toDataURL: async (): Promise<string> => 'data:image/png;base64,QR'
  }
}))

/** The stub playing the BFF. */
let stub: StubServer

/** The replies the stub gives, by `METHOD /path`; each test fills the ones it needs. */
let table: StubRouteTable

beforeEach(async () => {
  table = {}
  stub = await startTransport(routeTable(table))
})

afterEach(async () => {
  vi.unstubAllGlobals()
  await stub.close()
})

describe('TwoFactorCard', () => {
  it('offers to enable when two-factor is off', () => {
    const wrapper = mountAccount(TwoFactorCard, { props: { enabled: false } })

    expect(wrapper.find('[data-testid="account-2fa-enable"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="account-2fa-disable"]').exists()).toBe(false)
  })

  it('offers to disable when two-factor is on', () => {
    const wrapper = mountAccount(TwoFactorCard, { props: { enabled: true } })

    expect(wrapper.find('[data-testid="account-2fa-disable"]').exists()).toBe(true)
    expect(wrapper.text()).toContain('client.twoFactor.descEnabled')
  })

  it('opens the dialog with the secret once enrolment has started', async () => {
    table[`${HttpMethod.Post} ${AccountEndpoint.TwoFactorSetup}`] = { body: { secret: 'ABCD', qrCodeUri: 'otpauth://x' } }
    const wrapper = mountAccount(TwoFactorCard, { props: { enabled: false } })

    await wrapper.find('[data-testid="account-2fa-enable"]').trigger('click')

    await waitUntil(() => expect(wrapper.find('[data-testid="account-2fa-secret"]').exists()).toBe(true))
    expect(requestsTo(stub.requests, AccountEndpoint.TwoFactorSetup)).toHaveLength(1)
    expect(wrapper.find('[data-testid="account-2fa-secret"]').text()).toContain('ABCD')
    expect(wrapper.find('[data-testid="account-2fa-qr"]').attributes('src')).toBe('data:image/png;base64,QR')
  })

  it('keeps the dialog closed when enrolment could not start', async () => {
    table[`${HttpMethod.Post} ${AccountEndpoint.TwoFactorSetup}`] = refusal('Not now.')
    const wrapper = mountAccount(TwoFactorCard, { props: { enabled: false } })

    await wrapper.find('[data-testid="account-2fa-enable"]').trigger('click')

    await waitUntil(() => expect(requestsTo(stub.requests, AccountEndpoint.TwoFactorSetup)).toHaveLength(1))
    await flushPromises()
    expect(wrapper.find('[data-testid="account-2fa-dialog"]').exists()).toBe(false)
  })

  it('opens the dialog without a QR when disabling', async () => {
    const wrapper = mountAccount(TwoFactorCard, { props: { enabled: true } })

    await wrapper.find('[data-testid="account-2fa-disable"]').trigger('click')

    expect(wrapper.find('[data-testid="account-2fa-dialog"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="account-2fa-qr"]').exists()).toBe(false)
    expect(stub.requests).toHaveLength(0)
  })
})
