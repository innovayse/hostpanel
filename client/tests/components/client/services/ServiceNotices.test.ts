/**
 * Unit tests for ServiceNotices: the labelled notices region, the pending-cancellation banner with
 * its type, and the setup banner.
 */
import { describe, expect, it } from 'vitest'
import { mountClient } from '~/tests/helpers/mountClient'
import ServiceNotices from '~/components/client/services/ServiceNotices.vue'
import UiAlert from '~/components/ui/UiAlert.vue'
import { CancellationType } from '~/types/cancellationtype'
import { makeClientService } from '~/tests/helpers/clientServiceFixture'
import type { ClientService } from '~/types/clientservice'
import { ServiceStatus } from '~/types/servicestatus'
import { UiAlertVariant } from '~/utils/ui/enums'

/**
 * Builds a service in a status.
 *
 * @param status - The status.
 * @returns A service.
 */
const service = (status: ServiceStatus): ClientService => makeClientService({ status })

describe('ServiceNotices', () => {
  it('shows nothing for an ordinary active service', () => {
    const wrapper = mountClient(ServiceNotices, { props: { service: service(ServiceStatus.Active), serviceId: '4', cancelStatus: { pending: false } } })

    expect(wrapper.find('[data-testid="service-cancel-pending"]').exists()).toBe(false)
    expect(wrapper.find('[data-testid="service-setup-required"]').exists()).toBe(false)
  })

  it('leaves the notices region out when no notice shows', () => {
    const wrapper = mountClient(ServiceNotices, { props: { service: service(ServiceStatus.Active), serviceId: '4', cancelStatus: { pending: false } } })

    expect(wrapper.find('[data-testid="service-notices"]').exists()).toBe(false)
  })

  it('shows a pending cancellation as a warning, not an error', () => {
    const wrapper = mountClient(ServiceNotices, { props: { service: service(ServiceStatus.Active), serviceId: '4', cancelStatus: { pending: true, type: CancellationType.Immediate } } })

    const banner = wrapper.findAllComponents(UiAlert).find((alert) => alert.attributes('data-testid') === 'service-cancel-pending')
    expect(banner?.props('variant')).toBe(UiAlertVariant.Warning)
  })

  it('groups the notices in one region named by its heading', () => {
    const wrapper = mountClient(ServiceNotices, { props: { service: service(ServiceStatus.Pending), serviceId: '4', cancelStatus: { pending: true, type: CancellationType.Immediate } } })

    const region = wrapper.find('[data-testid="service-notices"]')
    expect(region.element.tagName).toBe('SECTION')
    const headingId = region.attributes('aria-labelledby')
    expect(headingId).toBeTruthy()
    const heading = region.find(`[id="${headingId}"]`)
    expect(heading.text()).toBe('client.services.noticesHeading')
    expect(region.find('[data-testid="service-cancel-pending"]').exists()).toBe(true)
    expect(region.find('[data-testid="service-setup-required"]').exists()).toBe(true)
  })

  it('renders the notices as static content, not live announcements', () => {
    const wrapper = mountClient(ServiceNotices, { props: { service: service(ServiceStatus.Pending), serviceId: '4', cancelStatus: { pending: true, type: CancellationType.Immediate } } })

    expect(wrapper.find('[data-testid="service-cancel-pending"]').attributes('role')).toBeUndefined()
    expect(wrapper.find('[data-testid="service-setup-required"]').attributes('role')).toBeUndefined()
  })

  it.each([
    [CancellationType.Immediate, 'client.services.cancelImmediate'],
    [CancellationType.EndOfBillingPeriod, 'client.services.cancelEndOfPeriod'],
  ])('names the %s cancellation', (type, key) => {
    const wrapper = mountClient(ServiceNotices, { props: { service: service(ServiceStatus.Active), serviceId: '4', cancelStatus: { pending: true, type } } })

    expect(wrapper.find('[data-testid="service-cancel-pending"]').text()).toContain(key)
  })

  it('shows the pending notice without a type when the API sent an unknown one', () => {
    const cancelStatus = { pending: true, type: 'Something' as CancellationType }
    const wrapper = mountClient(ServiceNotices, { props: { service: service(ServiceStatus.Active), serviceId: '4', cancelStatus } })

    const banner = wrapper.find('[data-testid="service-cancel-pending"]')
    expect(banner.exists()).toBe(true)
    expect(banner.text()).not.toContain('client.services.cancelImmediate')
    expect(banner.text()).not.toContain('client.services.cancelEndOfPeriod')
  })

  it('sends a pending service to its setup wizard', () => {
    const wrapper = mountClient(ServiceNotices, { props: { service: service(ServiceStatus.Pending), serviceId: '4', cancelStatus: { pending: false } } })

    expect(wrapper.find('[data-testid="service-setup-required"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="service-complete-setup"]').attributes('href')).toBe('/client/services/4/setup')
  })

  it('asks for no setup of a pending digital service, which has nothing to set up', () => {
    const wrapper = mountClient(ServiceNotices, { props: { service: makeClientService({ status: ServiceStatus.Pending, isDigital: true }), serviceId: '4', cancelStatus: { pending: false } } })

    expect(wrapper.find('[data-testid="service-setup-required"]').exists()).toBe(false)
    expect(wrapper.find('[data-testid="service-notices"]').exists()).toBe(false)
  })
})
