/**
 * Tests for `pages/client/confirm-email.vue` against the real auth store, the real auth API
 * composable and the real transport, with a stub BFF answering on a real socket (nothing of ours
 * is mocked): the three panels the confirmation walks through.
 *
 * @module tests/pages/client/confirm-email.test
 */

import { afterEach, beforeEach, describe, expect, it } from 'vitest'
import { settle } from '~/tests/helpers/settle'
import { mountAuthPage, stubAuthRuntime } from '~/tests/helpers/authScreens'
import { bodyOf } from '~/tests/helpers/requestBody'
import { requestsTo, routeTable, writeOf } from '~/tests/helpers/stubRoutes'
import type { StubRouteTable } from '~/tests/helpers/stubRoutes'
import { AccountEndpoint } from '~/tests/helpers/accountPaths'
import type { StubServer } from '~/tests/helpers/stubServer'
import { startTransport } from '~/tests/helpers/stubTransport'
import { CLIENT_LOGIN_PATH, CLIENT_REGISTER_PATH } from '~/constants/clientRoutes'
import ConfirmEmailPage from '~/pages/client/confirm-email.vue'
import { waitUntil } from '~/tests/helpers/waitUntil'

/** The stub playing the BFF. */
let stub: StubServer

/** The replies of the current test; a test changes them before it mounts. */
let table: StubRouteTable

beforeEach(async () => {
  table = { [writeOf(AccountEndpoint.ConfirmEmail)]: { body: {} } }
  stub = await startTransport(routeTable(table))
})

afterEach(async () => {
  await stub.close()
})

describe('client confirm-email page', () => {
  it('confirms the address from the link and offers sign-in', async () => {
    stubAuthRuntime({ query: { email: 'ada@example.com', token: 'tok' } })

    const wrapper = await mountAuthPage(ConfirmEmailPage)
    await waitUntil(() => expect(wrapper.find('[data-testid="confirm-success"]').exists()).toBe(true))

    expect(bodyOf(requestsTo(stub.requests, AccountEndpoint.ConfirmEmail)[0])).toEqual({ email: 'ada@example.com', token: 'tok' })
    expect(wrapper.find('[data-testid="confirm-signin"]').attributes('href')).toBe(CLIENT_LOGIN_PATH)
  })

  it('shows the API sentence and a way back to registration when it is refused', async () => {
    table[writeOf(AccountEndpoint.ConfirmEmail)] = { status: 400, body: { detail: 'This link has expired.' } }
    stubAuthRuntime({ query: { email: 'ada@example.com', token: 'tok' } })

    const wrapper = await mountAuthPage(ConfirmEmailPage)
    await waitUntil(() => expect(wrapper.find('[data-testid="confirm-failed"]').exists()).toBe(true))

    expect(wrapper.find('[data-testid="confirm-failed"]').text()).toContain('This link has expired.')
    expect(wrapper.find('[data-testid="confirm-register"]').attributes('href')).toBe(CLIENT_REGISTER_PATH)
  })

  it('reports an incomplete link without calling the API', async () => {
    stubAuthRuntime({ query: { email: 'ada@example.com' } })

    const wrapper = await mountAuthPage(ConfirmEmailPage)
    await waitUntil(() => expect(wrapper.find('[data-testid="confirm-failed"]').exists()).toBe(true))
    await settle()

    expect(stub.requests).toHaveLength(0)
    expect(wrapper.find('[data-testid="confirm-failed"]').text()).toContain('client.confirmEmail.invalidLink')
  })

  it('has no logo or heading of its own, only the outcome', async () => {
    stubAuthRuntime({ query: { email: 'ada@example.com', token: 'tok' } })

    const wrapper = await mountAuthPage(ConfirmEmailPage)
    await waitUntil(() => expect(wrapper.find('[data-testid="confirm-success"]').exists()).toBe(true))

    expect(wrapper.find('[data-testid="auth-logo"]').exists()).toBe(false)
    expect(wrapper.find('[data-testid="auth-shell-title"]').exists()).toBe(false)
  })
})
