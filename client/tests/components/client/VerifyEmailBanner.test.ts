/**
 * Unit tests for ClientVerifyEmailBanner against the real auth store, the real auth API composable
 * and the real transport, with a stub BFF answering on a real socket (nothing of ours is mocked): it
 * shows only for an explicitly unconfirmed address, and its button asks the auth store to send the
 * mail again, showing the sentence that results.
 *
 * @module tests/components/client/VerifyEmailBanner.test
 */

import type { VueWrapper } from '@vue/test-utils'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { mountClient } from '~/tests/helpers/mountClient'
import { settle } from '~/tests/helpers/settle'
import { requestsTo, routeTable } from '~/tests/helpers/stubRoutes'
import type { StubRouteTable } from '~/tests/helpers/stubRoutes'
import type { StubServer } from '~/tests/helpers/stubServer'
import { startTransport } from '~/tests/helpers/stubTransport'
import VerifyEmailBanner from '~/components/client/VerifyEmailBanner.vue'

/** The BFF paths the banner uses. */
const PATHS = {
  status: '/api/portal/auth/email-verified',
  resend: '/api/portal/auth/resend-verification',
} as const

/** The stub playing the BFF. */
let stub: StubServer

/** The replies of the current test; a test changes them before it mounts. */
let table: StubRouteTable

/**
 * Mounts the banner and waits until the status read has been answered and handled.
 *
 * @returns The wrapper.
 */
const mountBanner = async (): Promise<VueWrapper> => {
  const wrapper = mountClient(VerifyEmailBanner)
  await vi.waitFor(() => expect(requestsTo(stub.requests, PATHS.status)).toHaveLength(1))
  await settle()
  return wrapper
}

beforeEach(async () => {
  table = {}
  stub = await startTransport(routeTable(table))
})

afterEach(async () => {
  await stub.close()
})

describe('ClientVerifyEmailBanner', () => {
  it('is shown when the address is not confirmed', async () => {
    table[`GET ${PATHS.status}`] = { body: { verified: false } }

    const wrapper = await mountBanner()

    expect(wrapper.find('[data-testid="verify-email-banner"]').exists()).toBe(true)
  })

  it('is hidden when the address is confirmed', async () => {
    table[`GET ${PATHS.status}`] = { body: { verified: true } }

    const wrapper = await mountBanner()

    expect(wrapper.find('[data-testid="verify-email-banner"]').exists()).toBe(false)
  })

  it('is hidden when the answer is unknown, so a missing claim never nags', async () => {
    table[`GET ${PATHS.status}`] = { body: { verified: null } }

    const wrapper = await mountBanner()

    expect(wrapper.find('[data-testid="verify-email-banner"]').exists()).toBe(false)
  })

  it('asks for the mail again and confirms it was sent', async () => {
    table[`GET ${PATHS.status}`] = { body: { verified: false } }
    table[`POST ${PATHS.resend}`] = { body: {} }
    const wrapper = await mountBanner()

    await wrapper.find('[data-testid="verify-email-resend"]').trigger('click')
    await vi.waitFor(() => expect(wrapper.find('[data-testid="verify-email-message"]').text()).toContain('client.verifyEmail.sent'))

    expect(requestsTo(stub.requests, PATHS.resend)).toHaveLength(1)
  })

  it('shows the backend sentence when the resend is refused', async () => {
    table[`GET ${PATHS.status}`] = { body: { verified: false } }
    table[`POST ${PATHS.resend}`] = { status: 429, body: { detail: 'Too many requests.' } }
    const wrapper = await mountBanner()

    await wrapper.find('[data-testid="verify-email-resend"]').trigger('click')
    await vi.waitFor(() => expect(wrapper.find('[data-testid="verify-email-message"]').text()).toBe('Too many requests.'))
  })
})
