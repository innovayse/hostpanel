/**
 * Tests for `pages/client/services/index.vue`: the cards, the setup banner and the list states.
 */
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { mountPage, stubClientRuntime } from '~/tests/helpers/mountClient'
import ServicesPage from '~/pages/client/services/index.vue'
import { makeClientService } from '~/tests/helpers/clientServiceFixture'
import type { ClientService } from '~/types/clientservice'

/** The mocked client endpoints the store reaches. */
const clientApi = vi.hoisted(() => ({ fetchServices: vi.fn(), fetchMe: vi.fn() }))

vi.mock('~/composables/apis/useClientApi', () => ({ useClientApi: () => clientApi }))

/**
 * Builds a service with the fields the card reads.
 *
 * @param id - Service id.
 * @param username - Control-panel account; null for a service that still needs setup.
 * @returns A service.
 */
const service = (id: number, username: string | null = 'webmstr'): ClientService =>
  makeClientService({ id, productName: `Plan ${id}`, domain: `site${id}.com`, username })

describe('service list page', () => {
  beforeEach(() => {
    clientApi.fetchServices.mockReset()
    stubClientRuntime()
  })

  it('shows one card per service', async () => {
    clientApi.fetchServices.mockResolvedValue([service(1), service(2)])

    const wrapper = await mountPage(ServicesPage)

    expect(wrapper.findAll('[data-testid^="service-card-"]')).toHaveLength(2)
    expect(wrapper.find('[data-testid="services-setup-alert"]').exists()).toBe(false)
  })

  it('announces how many services still need setup', async () => {
    clientApi.fetchServices.mockResolvedValue([service(1), service(2, null), service(3, null)])

    const wrapper = await mountPage(ServicesPage)

    expect(wrapper.find('[data-testid="services-setup-alert"]').text()).toContain('client.dashboard.setupAlertSubtitle')
    expect(wrapper.findAll('[data-testid="service-needs-setup"]')).toHaveLength(2)
  })

  it('does not count a digital service among those that need setup', async () => {
    clientApi.fetchServices.mockResolvedValue([service(1), makeClientService({ id: 2, username: null, isDigital: true })])

    const wrapper = await mountPage(ServicesPage)

    expect(wrapper.find('[data-testid="services-setup-alert"]').exists()).toBe(false)
    expect(wrapper.findAll('[data-testid="service-needs-setup"]')).toHaveLength(0)
  })

  it('shows the empty state with a link to the hosting plans', async () => {
    clientApi.fetchServices.mockResolvedValue([])

    const wrapper = await mountPage(ServicesPage)

    expect(wrapper.find('[data-testid="services-browse-hosting"]').attributes('href')).toBe('/hosting')
  })

  it('shows the failure, not the empty state, when the read failed', async () => {
    clientApi.fetchServices.mockRejectedValue({ data: { detail: 'Hosting is down.' } })

    const wrapper = await mountPage(ServicesPage)

    expect(wrapper.find('[data-testid="services-error"]').text()).toBe('Hosting is down.')
    expect(wrapper.find('[data-testid="ui-empty-state"]').exists()).toBe(false)
  })
})
