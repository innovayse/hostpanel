/**
 * Unit tests for ClientForgotPasswordPanel against the real auth store, the real auth API
 * composable and the real transport, with a stub BFF answering on a real socket (nothing of ours
 * is mocked): the request form, its refusal and the "mail sent" state.
 *
 * @module tests/components/client/ForgotPasswordPanel.test
 */

import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { mountWithLogo } from '~/tests/helpers/authScreens'
import { bodyOf } from '~/tests/helpers/requestBody'
import { requestsTo, routeTable } from '~/tests/helpers/stubRoutes'
import type { StubRouteTable } from '~/tests/helpers/stubRoutes'
import type { StubServer } from '~/tests/helpers/stubServer'
import { startTransport } from '~/tests/helpers/stubTransport'
import ForgotPasswordPanel from '~/components/client/ForgotPasswordPanel.vue'

/** The BFF path the reset mail is requested from. */
const FORGOT_PATH = '/api/portal/auth/forgot-password'

/** The stub playing the BFF. */
let stub: StubServer

/** The replies of the current test; a test changes them before it submits. */
let table: StubRouteTable

/**
 * Types an address and submits the form.
 *
 * @param wrapper - The mounted panel.
 * @param email - Address to type.
 */
const submitWith = async (wrapper: ReturnType<typeof mountWithLogo>, email: string): Promise<void> => {
  await wrapper.find('[data-testid="forgot-email"]').setValue(email)
  await wrapper.find('form').trigger('submit')
}

beforeEach(async () => {
  table = { [`POST ${FORGOT_PATH}`]: { body: {} } }
  stub = await startTransport(routeTable(table))
})

afterEach(async () => {
  await stub.close()
})

describe('ClientForgotPasswordPanel', () => {
  it('shows the form with a placeholder on the email field', () => {
    const wrapper = mountWithLogo(ForgotPasswordPanel)

    expect(wrapper.find('[data-testid="forgot-form"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="forgot-email"]').attributes('placeholder')).toBe('client.forgot.emailPlaceholder')
  })

  it('asks the API for a reset mail and switches to the sent state, echoing the address', async () => {
    const wrapper = mountWithLogo(ForgotPasswordPanel)

    await submitWith(wrapper, 'ada@example.com')
    await vi.waitFor(() => expect(wrapper.find('[data-testid="outcome-panel"]').exists()).toBe(true))

    expect(bodyOf(requestsTo(stub.requests, FORGOT_PATH)[0])).toEqual({ email: 'ada@example.com' })
    expect(wrapper.find('[data-testid="forgot-form"]').exists()).toBe(false)
    expect(wrapper.find('[data-testid="forgot-sent-back"]').attributes('href')).toBe('/client/login')
  })

  it('keeps the form and shows the API sentence when the request is refused', async () => {
    table[`POST ${FORGOT_PATH}`] = { status: 429, body: { detail: 'Too many requests.' } }
    const wrapper = mountWithLogo(ForgotPasswordPanel)

    await submitWith(wrapper, 'ada@example.com')
    await vi.waitFor(() => expect(wrapper.find('[data-testid="ui-form-error"]').exists()).toBe(true))

    expect(wrapper.find('[data-testid="forgot-form"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="ui-form-error"]').text()).toBe('Too many requests.')
  })

  it('links back to sign-in from the form', () => {
    const wrapper = mountWithLogo(ForgotPasswordPanel)

    expect(wrapper.find('[data-testid="forgot-back"]').attributes('href')).toBe('/client/login')
  })
})
