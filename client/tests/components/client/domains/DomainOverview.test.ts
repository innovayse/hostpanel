/**
 * Unit tests for DomainOverview: the warning, the amounts in the domain's currency and the shortcuts.
 */
import { describe, expect, it } from 'vitest'
import { mountClient } from '~/tests/helpers/mountClient'
import { grantPermissions } from '~/tests/helpers/grantPermissions'
import { Permission } from '~/types/permission'
import DomainOverview from '~/components/client/domains/DomainOverview.vue'
import type { DomainDetail } from '~/types/domaindetail'
import { DomainTab } from '~/types/domaintab'
import { DomainStatus } from '~/types/domainstatus'

/** A domain with the fields the overview reads. */
const domain = {
  id: 1, name: 'example.com', status: DomainStatus.Active, registeredAt: '2025-03-05', expiresAt: '2027-03-05', nextDueDate: '2027-02-05',
  firstPaymentAmount: 15, recurringAmount: 12, registrationPeriod: 1, paymentMethod: 'card', priceCurrency: 'USD'
} as DomainDetail

/**
 * Mounts the overview with defaults.
 *
 * @param props - Props to override.
 * @returns The wrapper.
 */
const mountOverview = (props: Record<string, unknown> = {}) =>
  mountClient(DomainOverview, { props: { domain, fullName: 'example.com', lockEnabled: true, isPending: false, ...props } })

describe('DomainOverview', () => {
  it('formats the amounts in the currency of the domain', () => {
    const wrapper = mountOverview()

    expect(wrapper.text()).toContain('$15.00')
    expect(wrapper.text()).toContain('$12.00')
  })

  it('warns while the registrar lock is off', () => {
    expect(mountOverview().find('[data-testid="domain-unlock-warning"]').exists()).toBe(false)
    expect(mountOverview({ lockEnabled: false }).find('[data-testid="domain-unlock-warning"]').exists()).toBe(true)
  })

  it('shows neither the warning nor the shortcuts while the domain is pending', () => {
    const wrapper = mountOverview({ lockEnabled: false, isPending: true })

    expect(wrapper.find('[data-testid="domain-unlock-warning"]').exists()).toBe(false)
    expect(wrapper.find('[data-testid="domain-quick-actions"]').exists()).toBe(false)
  })

  it('opens the panel a shortcut names', async () => {
    const wrapper = mountOverview()

    await wrapper.find('[data-testid="domain-quick-renew"]').trigger('click')
    await wrapper.find('[data-testid="domain-quick-nameservers"]').trigger('click')

    expect(wrapper.emitted('navigate')).toEqual([[DomainTab.Renew], [DomainTab.Nameservers]])
  })
})

describe('DomainOverview permissions', () => {
  it('shows only the shortcuts the person may use', async () => {
    const wrapper = mountOverview()
    await grantPermissions(Permission.ViewDomains | Permission.PlaceNewOrders)

    expect(wrapper.find('[data-testid="domain-quick-renew"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="domain-quick-nameservers"]').exists()).toBe(false)
    expect(wrapper.find('[data-testid="domain-quick-lock"]').exists()).toBe(false)
  })

  it('drops the shortcuts card when the person may use none of them', async () => {
    const wrapper = mountOverview()
    await grantPermissions(Permission.ViewDomains)

    expect(wrapper.find('[data-testid="domain-quick-actions"]').exists()).toBe(false)
  })
})
