/**
 * Unit tests for ClientDashboardAlerts: the attention banners under the counters.
 *
 * @module tests/components/client/dashboard/Alerts.test
 */

import { describe, expect, it } from 'vitest'
import { nextTick } from 'vue'
import { mountClient } from '~/tests/helpers/mountClient'
import Alerts from '~/components/client/dashboard/Alerts.vue'
import { useClientStore } from '~/stores/client'
import type { ClientDomain } from '~/types/clientdomain'
import type { ClientInvoice } from '~/types/clientinvoice'
import type { ClientService } from '~/types/clientservice'
import { InvoiceStatus } from '~/types/invoicestatus'
import { ServiceStatus } from '~/types/servicestatus'

/** One day in milliseconds. */
const DAY_MS = 86_400_000

/**
 * Builds a domain expiring in the given number of days.
 *
 * @param id - Domain id.
 * @param days - Days until expiry.
 * @returns A domain row with only the fields the banners read.
 */
const domain = (id: number, days: number): ClientDomain =>
  ({ id, expiresAt: new Date(Date.now() + days * DAY_MS).toISOString() }) as unknown as ClientDomain

describe('ClientDashboardAlerts', () => {
  it('renders nothing when there is nothing to flag', () => {
    const wrapper = mountClient(Alerts)

    expect(wrapper.find('[data-testid="dashboard-alerts"]').exists()).toBe(false)
  })

  it('flags one service that needs setup with the singular title', async () => {
    const wrapper = mountClient(Alerts)
    useClientStore().services = [{ id: 1, status: ServiceStatus.Pending }] as unknown as ClientService[]
    await nextTick()

    const alert = wrapper.find('[data-testid="dashboard-alert-setup"]')
    expect(alert.text()).toContain('client.dashboard.setupAlertSingle')
    expect(wrapper.find('[data-testid="dashboard-alert-setup-action"]').attributes('href')).toBe('/client/services')
  })

  it('uses the plural title for several services', async () => {
    const wrapper = mountClient(Alerts)
    useClientStore().services = [{ id: 1, status: ServiceStatus.Pending }, { id: 2, status: ServiceStatus.Pending }] as unknown as ClientService[]
    await nextTick()

    expect(wrapper.find('[data-testid="dashboard-alert-setup"]').text()).toContain('client.dashboard.setupAlertMultiple')
  })

  it('flags unpaid invoices and links to the list', async () => {
    const wrapper = mountClient(Alerts)
    useClientStore().invoices = [{ id: 1, status: InvoiceStatus.Unpaid }] as unknown as ClientInvoice[]
    await nextTick()

    expect(wrapper.find('[data-testid="dashboard-alert-unpaid"]').text()).toContain('client.dashboard.unpaidSingle')
    expect(wrapper.find('[data-testid="dashboard-alert-unpaid-action"]').attributes('href')).toBe('/client/invoices')
  })

  it('flags domains close to expiry, singular for one', async () => {
    const wrapper = mountClient(Alerts)
    useClientStore().domains = [domain(1, 10)]
    await nextTick()

    expect(wrapper.find('[data-testid="dashboard-alert-expiring"]').text()).toContain('client.dashboard.expiringSoonSingle')
    expect(wrapper.find('[data-testid="dashboard-alert-expiring-action"]').attributes('href')).toBe('/client/domains')
  })

  it('uses the plural sentence for several domains', async () => {
    const wrapper = mountClient(Alerts)
    useClientStore().domains = [domain(1, 10), domain(2, 20)]
    await nextTick()

    expect(wrapper.find('[data-testid="dashboard-alert-expiring"]').text()).toContain('client.dashboard.expiringSoonMultiple')
  })

  it('waits for the domains before showing the expiry card', async () => {
    const wrapper = mountClient(Alerts)
    const store = useClientStore()
    store.domains = [domain(1, 10)]
    store.domainsLoading = true
    await nextTick()

    expect(wrapper.find('[data-testid="dashboard-alert-expiring"]').exists()).toBe(false)
  })
})
