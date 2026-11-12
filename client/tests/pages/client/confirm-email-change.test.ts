/**
 * Tests for `pages/client/confirm-email-change.vue` against the real account and auth stores, the
 * real API composables and the real transport, with a stub BFF answering on a real socket
 * (nothing of ours is mocked): the three panels the confirmation of a sign-in address change walks
 * through.
 *
 * @module tests/pages/client/confirm-email-change.test
 */

import { afterEach, beforeEach, describe, expect, it } from 'vitest'
import { settle } from '~/tests/helpers/settle'
import { mountAuthPage, stubAuthRuntime } from '~/tests/helpers/authScreens'
import { bodyOf } from '~/tests/helpers/requestBody'
import { requestsTo, routeTable } from '~/tests/helpers/stubRoutes'
import type { StubRouteTable } from '~/tests/helpers/stubRoutes'
import type { StubServer } from '~/tests/helpers/stubServer'
import { startTransport } from '~/tests/helpers/stubTransport'
import ConfirmEmailChangePage from '~/pages/client/confirm-email-change.vue'
import { waitUntil } from '~/tests/helpers/waitUntil'

/** The BFF paths the page uses: the confirmation, and the session clear it ends with. */
const PATHS = {
  confirm: '/api/portal/client/email-change/confirm',
  logout: '/api/portal/auth/logout',
} as const

/** The stub playing the BFF. */
let stub: StubServer

/** The replies of the current test; a test changes them before it mounts. */
let table: StubRouteTable

beforeEach(async () => {
  table = {
    [`POST ${PATHS.confirm}`]: { body: {} },
    [`POST ${PATHS.logout}`]: { body: {} },
  }
  stub = await startTransport(routeTable(table))
})

afterEach(async () => {
  await stub.close()
})

describe('client confirm-email-change page', () => {
  it('confirms the change from the link, ends this session and offers sign-in', async () => {
    stubAuthRuntime({ query: { email: 'new@example.com', token: 'tok' } })

    const wrapper = await mountAuthPage(ConfirmEmailChangePage)
    await waitUntil(() => expect(wrapper.find('[data-testid="confirm-email-change-success"]').exists()).toBe(true))

    expect(bodyOf(requestsTo(stub.requests, PATHS.confirm)[0])).toEqual({ email: 'new@example.com', token: 'tok' })
    expect(requestsTo(stub.requests, PATHS.logout)).toHaveLength(1)
    expect(wrapper.find('[data-testid="confirm-email-change-success"]').text()).toContain('client.emailChange.signInAgain')
    expect(wrapper.find('[data-testid="confirm-email-change-signin"]').attributes('href')).toBe('/client/login')
  })

  it('shows the API sentence and a way back to the account when it is refused', async () => {
    table[`POST ${PATHS.confirm}`] = { status: 400, body: { detail: 'This link has expired.' } }
    stubAuthRuntime({ query: { email: 'new@example.com', token: 'tok' } })

    const wrapper = await mountAuthPage(ConfirmEmailChangePage)
    await waitUntil(() => expect(wrapper.find('[data-testid="confirm-email-change-failed"]').exists()).toBe(true))

    expect(wrapper.find('[data-testid="confirm-email-change-failed"]').text()).toContain('This link has expired.')
    expect(wrapper.find('[data-testid="confirm-email-change-account"]').attributes('href')).toBe('/client/account')
    expect(requestsTo(stub.requests, PATHS.logout)).toHaveLength(0)
  })

  it('reports an incomplete link without calling the API', async () => {
    stubAuthRuntime({ query: { email: 'new@example.com' } })

    const wrapper = await mountAuthPage(ConfirmEmailChangePage)
    await waitUntil(() => expect(wrapper.find('[data-testid="confirm-email-change-failed"]').exists()).toBe(true))
    await settle()

    expect(stub.requests).toHaveLength(0)
    expect(wrapper.find('[data-testid="confirm-email-change-failed"]').text()).toContain('client.confirmEmail.invalidLink')
  })
})
