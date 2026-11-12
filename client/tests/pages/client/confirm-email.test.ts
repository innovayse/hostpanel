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
import { requestsTo, routeTable } from '~/tests/helpers/stubRoutes'
import type { StubRouteTable } from '~/tests/helpers/stubRoutes'
import type { StubServer } from '~/tests/helpers/stubServer'
import { startTransport } from '~/tests/helpers/stubTransport'
import ConfirmEmailPage from '~/pages/client/confirm-email.vue'
import { waitUntil } from '~/tests/helpers/waitUntil'

/** The BFF path the confirmation is posted to. */
const CONFIRM_PATH = '/api/portal/auth/confirm-email'

/** The stub playing the BFF. */
let stub: StubServer

/** The replies of the current test; a test changes them before it mounts. */
let table: StubRouteTable

beforeEach(async () => {
  table = { [`POST ${CONFIRM_PATH}`]: { body: {} } }
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

    expect(bodyOf(requestsTo(stub.requests, CONFIRM_PATH)[0])).toEqual({ email: 'ada@example.com', token: 'tok' })
    expect(wrapper.find('[data-testid="confirm-signin"]').attributes('href')).toBe('/client/login')
  })

  it('shows the API sentence and a way back to registration when it is refused', async () => {
    table[`POST ${CONFIRM_PATH}`] = { status: 400, body: { detail: 'This link has expired.' } }
    stubAuthRuntime({ query: { email: 'ada@example.com', token: 'tok' } })

    const wrapper = await mountAuthPage(ConfirmEmailPage)
    await waitUntil(() => expect(wrapper.find('[data-testid="confirm-failed"]').exists()).toBe(true))

    expect(wrapper.find('[data-testid="confirm-failed"]').text()).toContain('This link has expired.')
    expect(wrapper.find('[data-testid="confirm-register"]').attributes('href')).toBe('/client/register')
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
