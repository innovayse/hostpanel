/**
 * Tests for `pages/client/services/[id]/setup.vue`: the wizard, the already-set-up state and the
 * unavailable state.
 */
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { unref } from 'vue'
import { mountPage, stubClientRuntime } from '~/tests/helpers/mountClient'
import SetupPage from '~/pages/client/services/[id]/setup.vue'

/** The mocked client endpoints the store reaches. */
const clientApi = vi.hoisted(() => ({ fetchService: vi.fn() }))

vi.mock('~/composables/apis/useClientApi', () => ({ useClientApi: () => clientApi }))

/**
 * Builds a service.
 *
 * @param overrides - Fields to set.
 * @returns The service.
 */
const service = (overrides: Record<string, unknown> = {}) => ({ id: 4, name: 'Starter', status: 'Pending', username: '', domain: '', ...overrides })

describe('hosting setup page', () => {
  beforeEach(() => {
    clientApi.fetchService.mockReset()
  })

  it('shows the wizard for a service waiting for setup and sets the page title', async () => {
    clientApi.fetchService.mockResolvedValue(service())
    const { useHead } = stubClientRuntime({ params: { id: '4' } })

    const wrapper = await mountPage(SetupPage)

    expect(clientApi.fetchService).toHaveBeenCalledWith('4')
    expect(wrapper.find('[data-testid="setup-wizard"]').exists()).toBe(true)
    // The page hands `useHead` a computed title, which Nuxt unwraps; unwrap it the same way.
    expect(unref(useHead.mock.calls[0]?.[0].title)).toContain('client.setup.pageTitle')
  })

  it('says a configured service is already set up and links to its page', async () => {
    clientApi.fetchService.mockResolvedValue(service({ status: 'Active', username: 'webmstr', domain: 'example.com' }))
    stubClientRuntime({ params: { id: '4' } })

    const wrapper = await mountPage(SetupPage)

    expect(wrapper.find('[data-testid="setup-wizard"]').exists()).toBe(false)
    expect(wrapper.find('[data-testid="setup-go-manage"]').attributes('href')).toBe('/client/services/4')
  })

  it('says setup is not available when the service cannot be read', async () => {
    clientApi.fetchService.mockRejectedValue({ data: { detail: 'No such service.' } })
    stubClientRuntime({ params: { id: '4' } })

    const wrapper = await mountPage(SetupPage)

    expect(wrapper.find('[data-testid="setup-wizard"]').exists()).toBe(false)
    expect(wrapper.find('[data-testid="setup-back-to-services"]').attributes('href')).toBe('/client/services')
  })

  it('sends a digital service to its own page instead of offering a wizard', async () => {
    clientApi.fetchService.mockResolvedValue(service({ isDigital: true, username: null }))
    stubClientRuntime({ params: { id: '4' } })

    await mountPage(SetupPage)

    expect(navigateTo).toHaveBeenCalledWith('/client/services/4', { replace: true })
  })
})
