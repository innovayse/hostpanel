/**
 * Tests for `pages/client/register.vue` against the real auth store, API composable and transport,
 * with a stub BFF answering on a real socket (nothing of ours is mocked): the registration form,
 * its refusals and the redirect to sign-in.
 *
 * @module tests/pages/client/register.test
 */

import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { mountAuthPage, stubAuthRuntime } from '~/tests/helpers/authScreens'
import { bodyOf } from '~/tests/helpers/requestBody'
import { requestsTo, routeTable } from '~/tests/helpers/stubRoutes'
import type { StubRouteTable } from '~/tests/helpers/stubRoutes'
import type { StubServer } from '~/tests/helpers/stubServer'
import { startTransport } from '~/tests/helpers/stubTransport'
import RegisterPage from '~/pages/client/register.vue'
import { waitUntil } from '~/tests/helpers/waitUntil'

/** The BFF path the page posts the registration to. */
const REGISTER_PATH = '/api/portal/auth/register'

/** The stub playing the BFF. */
let stub: StubServer

/** The replies of the current test; a test changes them before it mounts. */
let table: StubRouteTable

/**
 * Fills every field and submits the form.
 *
 * @param wrapper - The mounted page.
 */
const submit = async (wrapper: Awaited<ReturnType<typeof mountAuthPage>>): Promise<void> => {
  await wrapper.find('[data-testid="register-firstname"]').setValue('Ada')
  await wrapper.find('[data-testid="register-lastname"]').setValue('Lovelace')
  await wrapper.find('[data-testid="register-email"]').setValue('ada@example.com')
  await wrapper.find('[data-testid="register-password"]').setValue('long-enough-pw')
  await wrapper.find('form').trigger('submit')
}

beforeEach(async () => {
  stubAuthRuntime()
  table = {}
  stub = await startTransport(routeTable(table))
})

afterEach(async () => {
  await stub.close()
})

describe('client register page', () => {
  it('gives every field a placeholder', async () => {
    const wrapper = await mountAuthPage(RegisterPage)

    for (const id of ['firstname', 'lastname', 'email', 'password']) {
      expect(wrapper.find(`[data-testid="register-${id}"]`).attributes('placeholder')).toMatch(/^client\.register\./)
    }
  })

  it('sends the typed fields to the API', async () => {
    table[`POST ${REGISTER_PATH}`] = { body: {} }
    const wrapper = await mountAuthPage(RegisterPage)

    await submit(wrapper)
    await waitUntil(() => expect(requestsTo(stub.requests, REGISTER_PATH)).toHaveLength(1))

    expect(bodyOf(requestsTo(stub.requests, REGISTER_PATH)[0])).toEqual({ firstname: 'Ada', lastname: 'Lovelace', email: 'ada@example.com', password: 'long-enough-pw' })
  })

  it('shows the success banner and schedules the redirect to sign-in', async () => {
    table[`POST ${REGISTER_PATH}`] = { body: {} }
    const timer = vi.spyOn(globalThis, 'setTimeout')
    const wrapper = await mountAuthPage(RegisterPage)

    await submit(wrapper)
    await waitUntil(() => expect(wrapper.find('[data-testid="ui-form-success"]').exists()).toBe(true))

    expect(wrapper.find('[data-testid="ui-form-success"]').text()).toBe('client.register.success')
    expect(timer).toHaveBeenCalledWith(expect.any(Function), 1500)
    timer.mockRestore()
  })

  it('shows the API sentence and no success banner when registration is refused', async () => {
    table[`POST ${REGISTER_PATH}`] = { status: 409, body: { detail: 'That email is taken.' } }
    const wrapper = await mountAuthPage(RegisterPage)

    await submit(wrapper)
    await waitUntil(() => expect(wrapper.find('[data-testid="ui-form-error"]').exists()).toBe(true))

    expect(wrapper.find('[data-testid="ui-form-error"]').text()).toBe('That email is taken.')
    expect(wrapper.find('[data-testid="ui-form-success"]').exists()).toBe(false)
  })

  it('shows a field message under its field', async () => {
    table[`POST ${REGISTER_PATH}`] = { status: 400, body: { errors: { password: ['Too short.'] } } }
    const wrapper = await mountAuthPage(RegisterPage)

    await submit(wrapper)
    await waitUntil(() => expect(wrapper.find('[data-testid="register-password"]').element.closest('div.w-full')?.textContent).toContain('Too short.'))
  })

  it('links back to sign-in', async () => {
    const wrapper = await mountAuthPage(RegisterPage)

    expect(wrapper.find('[data-testid="register-signin"]').attributes('href')).toBe('/client/login')
  })
})
