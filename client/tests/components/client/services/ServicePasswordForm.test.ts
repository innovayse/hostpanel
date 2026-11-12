/**
 * Unit tests for ServicePasswordForm: the local checks, the request through the store and the
 * success and refusal states.
 *
 * The real service management store, API composable and transport run against a stub server
 * playing the BFF; the assertions are on the requests that reached it.
 *
 * @module tests/components/client/services/ServicePasswordForm.test
 */
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { mountClient } from '~/tests/helpers/mountClient'
import ServicePasswordForm from '~/components/client/services/ServicePasswordForm.vue'
import { useServiceManagementStore } from '~/stores/servicemanagement'
import { bodyOf } from '~/tests/helpers/requestBody'
import { requestsTo, routeTable } from '~/tests/helpers/stubRoutes'
import type { StubRouteTable } from '~/tests/helpers/stubRoutes'
import type { StubServer } from '~/tests/helpers/stubServer'
import { startTransport } from '~/tests/helpers/stubTransport'

/** The BFF path that takes the new control-panel password of service 4. */
const PASSWORD_PATH = '/api/portal/client/services/4/change-password'

/** The stub playing the BFF. */
let stub: StubServer

/** The replies of the current test; a test changes them before it mounts. */
let table: StubRouteTable

/**
 * Types a password pair and submits the form.
 *
 * @param wrapper - The mounted form.
 * @param first - The new password.
 * @param second - The confirmation.
 */
const submitWith = async (wrapper: ReturnType<typeof mountClient>, first: string, second: string): Promise<void> => {
  await wrapper.find('[data-testid="service-new-password"]').setValue(first)
  await wrapper.find('[data-testid="service-confirm-password"]').setValue(second)
  await wrapper.find('form').trigger('submit')
}

describe('ServicePasswordForm', () => {
  beforeEach(async () => {
    table = { [`POST ${PASSWORD_PATH}`]: { body: {} } }
    stub = await startTransport(routeTable(table))
  })

  afterEach(async () => {
    await stub.close()
  })

  it('refuses a short password without calling the API', async () => {
    const wrapper = mountClient(ServicePasswordForm, { props: { serviceId: '4' } })

    await submitWith(wrapper, 'short', 'short')

    expect(wrapper.find('[data-testid="ui-form-error"]').text()).toContain('client.services.passwordTooShort')
    expect(stub.requests).toHaveLength(0)
  })

  it('refuses a mismatching confirmation without calling the API', async () => {
    const wrapper = mountClient(ServicePasswordForm, { props: { serviceId: '4' } })

    await submitWith(wrapper, 'longenough1', 'longenough2')

    expect(wrapper.find('[data-testid="ui-form-error"]').text()).toContain('client.services.passwordMismatch')
    expect(stub.requests).toHaveLength(0)
  })

  it('sends a good password for this service and shows the success state', async () => {
    const wrapper = mountClient(ServicePasswordForm, { props: { serviceId: '4' } })

    await submitWith(wrapper, 'longenough1', 'longenough1')
    await vi.waitFor(() => expect(wrapper.find('[data-testid="service-password-success"]').exists()).toBe(true))

    expect(requestsTo(stub.requests, PASSWORD_PATH)).toHaveLength(1)
    expect(bodyOf(requestsTo(stub.requests, PASSWORD_PATH)[0])).toEqual({ password: 'longenough1' })
  })

  it('shows the API refusal above the form', async () => {
    table[`POST ${PASSWORD_PATH}`] = { status: 400, body: { detail: 'Password too common.' } }
    const wrapper = mountClient(ServicePasswordForm, { props: { serviceId: '4' } })

    await submitWith(wrapper, 'longenough1', 'longenough1')
    await vi.waitFor(() => expect(useServiceManagementStore().passwordError).toBe('Password too common.'))
    await wrapper.vm.$nextTick()

    expect(wrapper.find('[data-testid="ui-form-error"]').text()).toContain('Password too common.')
  })
})
