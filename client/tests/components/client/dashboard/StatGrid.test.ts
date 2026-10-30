/**
 * Unit tests for ClientDashboardStatGrid: the four counters read from the client store.
 *
 * @module tests/components/client/dashboard/StatGrid.test
 */

import { describe, expect, it } from 'vitest'
import { nextTick } from 'vue'
import { mountClient } from '~/tests/helpers/mountClient'
import { grantPermissions } from '~/tests/helpers/grantPermissions'
import { Permission } from '~/composables/usePermissions'
import StatGrid from '~/components/client/dashboard/StatGrid.vue'
import { useClientStore } from '~/stores/client'
import type { ClientDomain } from '~/types/clientdomain'
import type { ClientInvoice } from '~/types/clientinvoice'
import type { ClientService } from '~/types/clientservice'
import type { ClientTicket } from '~/types/clientticket'
import { DashboardStat } from '~/types/dashboardstat'
import { InvoiceStatus } from '~/types/invoicestatus'
import { ServiceStatus } from '~/types/servicestatus'
import { TicketStatus } from '~/types/ticketstatus'

/**
 * Reads the number a tile shows.
 *
 * @param wrapper - The mounted grid.
 * @param stat - Which tile.
 * @returns The tile's value text.
 */
const valueOf = (wrapper: ReturnType<typeof mountClient>, stat: DashboardStat): string =>
  wrapper.find(`[data-testid="dashboard-stat-${stat}"] [data-testid="ui-stat-tile-value"]`).text()

describe('ClientDashboardStatGrid', () => {
  it('counts active services, domains, unpaid invoices and open tickets', async () => {
    const wrapper = mountClient(StatGrid, { props: { loading: false } })
    const store = useClientStore()
    store.services = [{ status: ServiceStatus.Active }, { status: ServiceStatus.Active }, { status: ServiceStatus.Pending }] as unknown as ClientService[]
    store.domains = [{ id: 1 }] as unknown as ClientDomain[]
    store.invoices = [{ status: InvoiceStatus.Unpaid }, { status: InvoiceStatus.Paid }] as unknown as ClientInvoice[]
    store.tickets = [{ status: TicketStatus.Open }, { status: TicketStatus.Answered }, { status: TicketStatus.Closed }] as unknown as ClientTicket[]
    await nextTick()

    expect(valueOf(wrapper, DashboardStat.Services)).toBe('2')
    expect(valueOf(wrapper, DashboardStat.Domains)).toBe('1')
    expect(valueOf(wrapper, DashboardStat.Invoices)).toBe('1')
    expect(valueOf(wrapper, DashboardStat.Tickets)).toBe('2')
  })

  it('shows dashes while loading', () => {
    const wrapper = mountClient(StatGrid, { props: { loading: true } })

    for (const stat of Object.values(DashboardStat)) {
      expect(valueOf(wrapper, stat)).toBe('—')
    }
  })

  it('links each tile to its section', () => {
    const wrapper = mountClient(StatGrid, { props: { loading: false } })

    expect(wrapper.find(`[data-testid="dashboard-stat-${DashboardStat.Services}"]`).attributes('href')).toBe('/client/services')
    expect(wrapper.find(`[data-testid="dashboard-stat-${DashboardStat.Domains}"]`).attributes('href')).toBe('/client/domains')
    expect(wrapper.find(`[data-testid="dashboard-stat-${DashboardStat.Invoices}"]`).attributes('href')).toBe('/client/invoices')
    expect(wrapper.find(`[data-testid="dashboard-stat-${DashboardStat.Tickets}"]`).attributes('href')).toBe('/client/tickets')
  })

  it('flags unpaid invoices as needing action and says nothing when there are none', async () => {
    const wrapper = mountClient(StatGrid, { props: { loading: false } })
    expect(wrapper.text()).not.toContain('client.dashboard.statActionRequired')

    useClientStore().invoices = [{ status: InvoiceStatus.Overdue }] as unknown as ClientInvoice[]
    await nextTick()

    expect(wrapper.text()).toContain('client.dashboard.statActionRequired')
  })
})

describe('ClientDashboardStatGrid permissions', () => {
  it('shows a counter only for the sections the person may see', async () => {
    const wrapper = mountClient(StatGrid, { props: { loading: false } })
    await grantPermissions(Permission.ViewDomains | Permission.ViewOpenSupportTickets)

    expect(wrapper.find(`[data-testid="dashboard-stat-${DashboardStat.Domains}"]`).exists()).toBe(true)
    expect(wrapper.find(`[data-testid="dashboard-stat-${DashboardStat.Tickets}"]`).exists()).toBe(true)
    expect(wrapper.find(`[data-testid="dashboard-stat-${DashboardStat.Services}"]`).exists()).toBe(false)
    expect(wrapper.find(`[data-testid="dashboard-stat-${DashboardStat.Invoices}"]`).exists()).toBe(false)
  })
})
