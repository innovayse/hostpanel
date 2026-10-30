/**
 * Unit tests for ServiceCard: where the card links, the setup badge, the renewal date, the amount
 * and the disk bar.
 */
import { describe, expect, it } from 'vitest'
import { mountClient } from '~/tests/helpers/mountClient'
import ServiceCard from '~/components/client/services/ServiceCard.vue'
import { EMPTY_DATE } from '~/utils/formatDate'
import { makeClientService as service } from '~/tests/helpers/clientServiceFixture'

describe('ServiceCard', () => {
  it('links a set-up service to its page', () => {
    const wrapper = mountClient(ServiceCard, { props: { service: service(), currency: { code: 'USD' } } })

    expect(wrapper.attributes('href')).toBe('/client/services/4')
    expect(wrapper.text()).toContain('Starter')
    expect(wrapper.text()).toContain('example.com')
  })

  it('links a service without a control-panel account to its setup wizard and flags it', () => {
    const wrapper = mountClient(ServiceCard, { props: { service: service({ username: null }), currency: {} } })

    expect(wrapper.attributes('href')).toBe('/client/services/4/setup')
    expect(wrapper.find('[data-testid="service-needs-setup"]').exists()).toBe(true)
  })

  it('links a digital service to its page, with its status and no setup flag', () => {
    const wrapper = mountClient(ServiceCard, { props: { service: service({ isDigital: true, username: null }), currency: {} } })

    expect(wrapper.attributes('href')).toBe('/client/services/4')
    expect(wrapper.find('[data-testid="service-needs-setup"]').exists()).toBe(false)
    expect(wrapper.text()).toContain('Active')
  })

  it('shows the status badge for a set-up service', () => {
    const wrapper = mountClient(ServiceCard, { props: { service: service(), currency: {} } })

    expect(wrapper.find('[data-testid="service-needs-setup"]').exists()).toBe(false)
    expect(wrapper.text()).toContain('Active')
  })

  it('words a service without a domain instead of leaving the line blank', () => {
    const wrapper = mountClient(ServiceCard, { props: { service: service({ domain: null }), currency: {} } })

    expect(wrapper.text()).toContain('client.dashboard.noDomain')
  })

  it('shows the empty date for a service not active yet', () => {
    const wrapper = mountClient(ServiceCard, { props: { service: service({ nextRenewalAt: null }), currency: {} } })

    expect(wrapper.text()).toContain(EMPTY_DATE)
  })

  it('formats the amount in the account currency', () => {
    const wrapper = mountClient(ServiceCard, { props: { service: service(), currency: { code: 'USD' } } })

    expect(wrapper.text()).toContain('$10.00')
  })

  it('draws the disk bar only when the server reported a disk limit', () => {
    const limited = mountClient(ServiceCard, { props: { service: service({ usage: { diskUsedMb: 256, diskLimitMb: 1024, bandwidthUsedMb: 10, bandwidthLimitMb: 0, updatedAt: '2026-10-01T00:00:00Z' } }), currency: {} } })
    const unlimited = mountClient(ServiceCard, { props: { service: service({ usage: { ...{ diskUsedMb: 256, diskLimitMb: 1024, bandwidthUsedMb: 10, bandwidthLimitMb: 0, updatedAt: '2026-10-01T00:00:00Z' }, diskLimitMb: 0 } }), currency: {} } })

    expect(limited.find('[data-testid="usage-bar"]').exists()).toBe(true)
    expect(unlimited.find('[data-testid="usage-bar"]').exists()).toBe(false)
  })
})
