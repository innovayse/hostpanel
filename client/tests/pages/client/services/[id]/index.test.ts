/**
 * Tests for `pages/client/services/[id]/index.vue`: the panels, the notices, the control-panel
 * sign-in and the redirect of a service that has no account yet, and a failed read: not found, or the API sentence with a retry.
 */
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { flushPromises } from '@vue/test-utils'
import { mountPage, stubClientRuntime } from '~/tests/helpers/mountClient'
import ServicePage from '~/pages/client/services/[id]/index.vue'
import { Permission } from '~/composables/usePermissions'
import { PortalErrorCode } from '~/constants/portalErrorCode'
import { makeClientService as service } from '~/tests/helpers/clientServiceFixture'
import { ServiceStatus } from '~/types/servicestatus'
import { SuspensionSource } from '~/types/suspensionsource'

/** The mocked client endpoints the stores reach. */
const clientApi = vi.hoisted(() => ({
  fetchService: vi.fn(), fetchHostingInfo: vi.fn(), fetchCancellationStatus: vi.fn(), fetchServiceInvoices: vi.fn(),
  fetchCpanelSsoUrl: vi.fn(), fetchMe: vi.fn()
}))

vi.mock('~/composables/apis/useClientApi', () => ({ useClientApi: () => clientApi }))

/** Stands in for Nuxt's `navigateTo`. */
const navigate = vi.fn()


describe('service page', () => {
  beforeEach(() => {
    Object.values(clientApi).forEach((fn) => fn.mockReset())
    clientApi.fetchService.mockResolvedValue(service())
    clientApi.fetchHostingInfo.mockResolvedValue({ nameservers: ['ns1.example.net'], ssl: null })
    clientApi.fetchCancellationStatus.mockResolvedValue({ pending: false })
    clientApi.fetchServiceInvoices.mockResolvedValue({ invoices: [], unattributedInvoiceCount: 0 })
    clientApi.fetchMe.mockResolvedValue({ currency: 'USD', permissions: Permission.All })
    navigate.mockReset()
    vi.stubGlobal('navigateTo', navigate)
    stubClientRuntime({ params: { id: '4' } })
  })

  it('reads the service, its hosting info and its cancellation status', async () => {
    await mountPage(ServicePage)

    expect(clientApi.fetchService).toHaveBeenCalledWith('4')
    expect(clientApi.fetchHostingInfo).toHaveBeenCalledWith('4')
    expect(clientApi.fetchCancellationStatus).toHaveBeenCalledWith('4')
  })

  it('opens on the overview with the details, the server and the invoices', async () => {
    const wrapper = await mountPage(ServicePage)

    expect(wrapper.find('[data-testid="service-details"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="service-server"]').text()).toContain('ns1.example.net')
    expect(wrapper.find('[data-testid="service-invoices"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="service-suspension-reason"]').exists()).toBe(false)
  })

  it('names why a suspended service is suspended', async () => {
    clientApi.fetchService.mockResolvedValue(service({ status: ServiceStatus.Suspended, suspensionSource: SuspensionSource.Overdue }))

    const wrapper = await mountPage(ServicePage)

    expect(wrapper.find('[data-testid="service-suspension-reason"]').text()).toContain('client.services.suspensionSources.overdue')
  })

  it('hides a leftover suspension source once the service is active again', async () => {
    clientApi.fetchService.mockResolvedValue(service({ status: ServiceStatus.Active, suspensionSource: SuspensionSource.Overdue }))

    const wrapper = await mountPage(ServicePage)

    expect(wrapper.find('[data-testid="service-suspension-reason"]').exists()).toBe(false)
  })

  it('switches to the password and the cancellation panels', async () => {
    const wrapper = await mountPage(ServicePage)

    await wrapper.find('[data-testid="service-tab-password"]').trigger('click')
    expect(wrapper.find('[data-testid="service-password"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="service-overview"]').exists()).toBe(false)

    await wrapper.find('[data-testid="service-tab-cancel"]').trigger('click')
    expect(wrapper.find('[data-testid="service-cancel"]').exists()).toBe(true)
  })

  it('opens the cancellation panel from the quick action', async () => {
    const wrapper = await mountPage(ServicePage)

    await wrapper.find('[data-testid="service-quick-cancel"]').trigger('click')

    expect(wrapper.find('[data-testid="service-cancel"]').exists()).toBe(true)
  })

  it('sends a service without a control-panel account to its setup wizard', async () => {
    clientApi.fetchService.mockResolvedValue(service({ username: null, status: ServiceStatus.Pending }))

    await mountPage(ServicePage)

    expect(navigate).toHaveBeenCalledWith('/client/services/4/setup')
  })

  it('keeps a digital service on its page, which has nothing to set up and no control-panel account', async () => {
    clientApi.fetchService.mockResolvedValue(service({ username: null, status: ServiceStatus.Pending, isDigital: true }))

    await mountPage(ServicePage)

    expect(navigate).not.toHaveBeenCalled()
  })

  it('does not send a terminated service to setup', async () => {
    clientApi.fetchService.mockResolvedValue(service({ username: null, status: ServiceStatus.Terminated }))

    await mountPage(ServicePage)

    expect(navigate).not.toHaveBeenCalled()
  })

  it('says the service was not found when the read is refused as not found', async () => {
    clientApi.fetchService.mockRejectedValue({ data: { data: { code: PortalErrorCode.ServiceNotFound } } })

    const wrapper = await mountPage(ServicePage)

    expect(wrapper.find('[data-testid="service-not-found"]').text()).toContain('client.services.notFound')
    expect(wrapper.find('[data-testid="service-load-error"]').exists()).toBe(false)
  })

  it('shows the API sentence and a retry when the read fails for another reason, and reads again on retry', async () => {
    clientApi.fetchService.mockRejectedValueOnce({ data: { detail: 'The service could not be read.' } })

    const wrapper = await mountPage(ServicePage)

    expect(wrapper.find('[data-testid="service-load-error"]').text()).toContain('The service could not be read.')
    expect(wrapper.find('[data-testid="service-not-found"]').exists()).toBe(false)

    await wrapper.find('[data-testid="service-retry"]').trigger('click')
    await flushPromises()

    expect(clientApi.fetchService).toHaveBeenCalledTimes(2)
    expect(wrapper.find('[data-testid="service-load-error"]').exists()).toBe(false)
    expect(wrapper.find('[data-testid="service-details"]').exists()).toBe(true)
  })
})

describe('service page permissions', () => {
  beforeEach(() => {
    Object.values(clientApi).forEach((fn) => fn.mockReset())
    clientApi.fetchService.mockResolvedValue(service())
    clientApi.fetchHostingInfo.mockResolvedValue({ nameservers: ['ns1.example.net'], ssl: null })
    clientApi.fetchCancellationStatus.mockResolvedValue({ pending: false })
    clientApi.fetchServiceInvoices.mockResolvedValue({ invoices: [], unattributedInvoiceCount: 0 })
    vi.stubGlobal('navigateTo', navigate)
    stubClientRuntime({ params: { id: '4' } })
  })

  it('shows neither the invoices nor the password and cancel panels to a person who may only view services', async () => {
    clientApi.fetchMe.mockResolvedValue({ currency: 'USD', permissions: Permission.ViewProductsServices })

    const wrapper = await mountPage(ServicePage)

    expect(wrapper.find('[data-testid="service-details"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="service-invoices"]').exists()).toBe(false)
    expect(clientApi.fetchServiceInvoices).not.toHaveBeenCalled()
    expect(wrapper.find('[data-testid="service-tab-password"]').exists()).toBe(false)
    expect(wrapper.find('[data-testid="service-tab-cancel"]').exists()).toBe(false)
    expect(wrapper.find('[data-testid="service-quick-cancel"]').exists()).toBe(false)
  })

  it('shows the invoices to a person who may pay invoices', async () => {
    clientApi.fetchMe.mockResolvedValue({ currency: 'USD', permissions: Permission.ViewProductsServices | Permission.ViewPayInvoices })

    const wrapper = await mountPage(ServicePage)

    expect(wrapper.find('[data-testid="service-invoices"]').exists()).toBe(true)
    expect(clientApi.fetchServiceInvoices).toHaveBeenCalledWith('4')
  })
})
