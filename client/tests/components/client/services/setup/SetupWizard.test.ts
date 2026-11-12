/**
 * Unit tests for SetupWizard: the step flow, the checks that gate it and the provisioning call.
 *
 * The real service management store, API composable and transport run against a stub server
 * playing the BFF; the assertions are on the request that reached it.
 *
 * @module tests/components/client/services/setup/SetupWizard.test
 */
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { mountClient, stubClientRuntime } from '~/tests/helpers/mountClient'
import SetupWizard from '~/components/client/services/setup/SetupWizard.vue'
import { makeClientService } from '~/tests/helpers/clientServiceFixture'
import { bodyOf } from '~/tests/helpers/requestBody'
import { requestsTo, routeTable } from '~/tests/helpers/stubRoutes'
import type { StubRouteTable } from '~/tests/helpers/stubRoutes'
import type { StubServer } from '~/tests/helpers/stubServer'
import { startTransport } from '~/tests/helpers/stubTransport'
import type { ClientService } from '~/types/clientservice'
import { ServiceStatus } from '~/types/servicestatus'
import { waitUntil } from '~/tests/helpers/waitUntil'

/** The BFF path that takes the first-time setup of service 4. */
const SETUP_PATH = '/api/portal/client/services/4/setup'

/** Stands in for Nuxt's `navigateTo`, so a test can see where the wizard went. */
const navigate = vi.fn()

/** The stub playing the BFF. */
let stub: StubServer

/** The replies of the current test; a test changes them before it mounts. */
let table: StubRouteTable

/** A service waiting for setup. */
const service = makeClientService({ domain: null, status: ServiceStatus.Pending, username: null })

/**
 * Mounts the wizard.
 *
 * @param overrides - Service fields to override.
 * @returns The wrapper.
 */
const mountWizard = (overrides: Partial<ClientService> = {}) =>
  mountClient(SetupWizard, { props: { service: { ...service, ...overrides }, serviceId: '4' } })

describe('SetupWizard', () => {
  beforeEach(async () => {
    navigate.mockReset()
    vi.stubGlobal('navigateTo', navigate)
    stubClientRuntime()
    table = { [`POST ${SETUP_PATH}`]: { body: {} } }
    stub = await startTransport(routeTable(table))
  })

  afterEach(async () => {
    await stub.close()
  })

  it('starts on the domain step and cannot continue without a domain', () => {
    const wrapper = mountWizard()

    expect(wrapper.find('[data-testid="setup-step-domain"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="setup-domain-next"]').attributes('disabled')).toBeDefined()
  })

  it('starts with the domain bought with the plan, locked', () => {
    const wrapper = mountWizard({ domain: 'example.com' })

    expect(wrapper.find('[data-testid="setup-domain"]').attributes('disabled')).toBeDefined()
    expect(wrapper.find('[data-testid="setup-domain-next"]').attributes('disabled')).toBeUndefined()
  })

  it('refuses a malformed domain with the checks of the server', async () => {
    const wrapper = mountWizard()

    await wrapper.find('[data-testid="setup-domain"]').setValue('notadomain')

    expect(wrapper.text()).toContain('client.setup.domainInvalid')
    expect(wrapper.find('[data-testid="setup-domain-next"]').attributes('disabled')).toBeDefined()
  })

  it('walks the three steps and provisions with what was typed', async () => {
    const wrapper = mountWizard()

    await wrapper.find('[data-testid="setup-domain"]').setValue('example.com')
    await wrapper.find('[data-testid="setup-domain-next"]').trigger('click')
    await wrapper.find('[data-testid="setup-username"]').setValue('Webmstr')
    await wrapper.find('[data-testid="setup-password"]').setValue('longenough')
    await wrapper.find('[data-testid="setup-credentials-next"]').trigger('click')
    await wrapper.find('[data-testid="setup-build"]').trigger('click')

    await waitUntil(() => expect(requestsTo(stub.requests, SETUP_PATH)).toHaveLength(1))
    expect(bodyOf(requestsTo(stub.requests, SETUP_PATH)[0])).toEqual({ domain: 'example.com', username: 'webmstr', password: 'longenough' })
    await waitUntil(() => expect(navigate).toHaveBeenCalledWith('/client/services/4'))
  })

  it('goes back a step and keeps what was typed', async () => {
    const wrapper = mountWizard()

    await wrapper.find('[data-testid="setup-domain"]').setValue('example.com')
    await wrapper.find('[data-testid="setup-domain-next"]').trigger('click')
    await wrapper.find('[data-testid="setup-credentials-back"]').trigger('click')

    expect((wrapper.find('[data-testid="setup-domain"]').element as HTMLInputElement).value).toBe('example.com')
  })

  it('stays on the review step and shows the refusal when provisioning fails', async () => {
    table[`POST ${SETUP_PATH}`] = { status: 409, body: { detail: 'Username taken.' } }
    const wrapper = mountWizard({ domain: 'example.com' })

    await wrapper.find('[data-testid="setup-domain-next"]').trigger('click')
    await wrapper.find('[data-testid="setup-username"]').setValue('webmstr')
    await wrapper.find('[data-testid="setup-password"]').setValue('longenough')
    await wrapper.find('[data-testid="setup-credentials-next"]').trigger('click')
    await wrapper.find('[data-testid="setup-build"]').trigger('click')

    await waitUntil(() => expect(wrapper.find('[data-testid="setup-error"]').text()).toBe('Username taken.'))
    expect(navigate).not.toHaveBeenCalled()
  })
})
