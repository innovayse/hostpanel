/**
 * Tests for `pages/client/reset-password.vue` against the real auth store, API composable and
 * transport, with a stub BFF answering on a real socket (nothing of ours is mocked): the
 * invalid-link state, the mismatch check, the request that reaches the API and its outcomes.
 *
 * @module tests/pages/client/reset-password.test
 */

import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { mountAuthPage, stubAuthRuntime } from '~/tests/helpers/authScreens'
import { bodyOf } from '~/tests/helpers/requestBody'
import { requestsTo, routeTable } from '~/tests/helpers/stubRoutes'
import type { StubRouteTable } from '~/tests/helpers/stubRoutes'
import type { StubServer } from '~/tests/helpers/stubServer'
import { startTransport } from '~/tests/helpers/stubTransport'
import ResetPasswordPage from '~/pages/client/reset-password.vue'

/** The BFF path the page posts the new password to. */
const RESET_PATH = '/api/portal/auth/reset-password'

/** A valid reset link's query. */
const LINK = { token: 'tok', email: 'ada@example.com' }

/** The stub playing the BFF. */
let stub: StubServer

/** The replies of the current test; a test changes them before it mounts. */
let table: StubRouteTable

/**
 * Types both password fields and submits.
 *
 * @param wrapper - The mounted page.
 * @param first - New password.
 * @param second - Confirmation.
 */
const submit = async (wrapper: Awaited<ReturnType<typeof mountAuthPage>>, first: string, second: string): Promise<void> => {
  await wrapper.find('[data-testid="reset-new-password"]').setValue(first)
  await wrapper.find('[data-testid="reset-confirm-password"]').setValue(second)
  await wrapper.find('form').trigger('submit')
}

beforeEach(async () => {
  table = {}
  stub = await startTransport(routeTable(table))
})

afterEach(async () => {
  await stub.close()
})

describe('client reset-password page', () => {
  it.each([
    ['no token', { email: 'ada@example.com' }],
    ['no address', { token: 'tok' }],
    ['neither', {}]
  ])('shows the invalid-link state with %s', async (_name, query) => {
    stubAuthRuntime({ query })

    const wrapper = await mountAuthPage(ResetPasswordPage)

    expect(wrapper.find('[data-testid="reset-invalid"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="reset-form"]').exists()).toBe(false)
    expect(wrapper.find('[data-testid="reset-request-new"]').attributes('href')).toBe('/client/forgot')
  })

  it('shows the form with placeholders for a valid link', async () => {
    stubAuthRuntime({ query: LINK })

    const wrapper = await mountAuthPage(ResetPasswordPage)

    expect(wrapper.find('[data-testid="reset-new-password"]').attributes('placeholder')).toBe('client.resetPassword.newPasswordPlaceholder')
    expect(wrapper.find('[data-testid="reset-confirm-password"]').attributes('placeholder')).toBe('client.resetPassword.confirmPasswordPlaceholder')
  })

  it('refuses mismatching passwords without calling the API', async () => {
    stubAuthRuntime({ query: LINK })
    const wrapper = await mountAuthPage(ResetPasswordPage)

    await submit(wrapper, 'one-password', 'another-password')

    await vi.waitFor(() => expect(wrapper.find('[data-testid="ui-form-error"]').exists()).toBe(true))
    expect(wrapper.find('[data-testid="ui-form-error"]').text()).toBe('client.resetPassword.errorMismatch')
    expect(stub.requests).toHaveLength(0)
  })

  it('sends the address, token and new password and shows the success state', async () => {
    stubAuthRuntime({ query: LINK })
    table[`POST ${RESET_PATH}`] = { body: {} }
    const wrapper = await mountAuthPage(ResetPasswordPage)

    await submit(wrapper, 'new-password-1', 'new-password-1')
    await vi.waitFor(() => expect(wrapper.find('[data-testid="reset-success"]').exists()).toBe(true))

    expect(bodyOf(requestsTo(stub.requests, RESET_PATH)[0])).toEqual({ email: 'ada@example.com', token: 'tok', newPassword: 'new-password-1' })
    expect(wrapper.find('[data-testid="reset-success-signin"]').attributes('href')).toBe('/client/login')
  })

  it('shows the API sentence and keeps the form when the reset is refused', async () => {
    stubAuthRuntime({ query: LINK })
    table[`POST ${RESET_PATH}`] = { status: 400, body: { detail: 'This link has expired.' } }
    const wrapper = await mountAuthPage(ResetPasswordPage)

    await submit(wrapper, 'new-password-1', 'new-password-1')
    await vi.waitFor(() => expect(wrapper.find('[data-testid="ui-form-error"]').exists()).toBe(true))

    expect(wrapper.find('[data-testid="ui-form-error"]').text()).toBe('This link has expired.')
    expect(wrapper.find('[data-testid="reset-success"]').exists()).toBe(false)
  })
})
