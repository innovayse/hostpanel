/**
 * Tests for DashboardServerLoad: a bar per server with a known share, most loaded first.
 */
import { describe, expect, it } from 'vitest'
import DashboardServerLoad from '@/components/dashboard/DashboardServerLoad.vue'
import type { ServerDto } from '@/types/server'
import { mountWithApp } from '../../helpers/mount'

/**
 * Builds a server with only the fields the card reads.
 *
 * @param id - Server id.
 * @param name - Display name.
 * @param accountsCount - Accounts from the last connection check.
 * @param maxAccounts - Capacity limit.
 * @returns The server.
 */
const server = (id: number, name: string, accountsCount: number | null, maxAccounts: number | null): ServerDto =>
  ({ id, name, accountsCount, limits: { maxAccounts, isDefault: false, isDisabled: false } }) as ServerDto

describe('DashboardServerLoad', () => {
  it('shows a bar per measurable server, most loaded first', () => {
    const wrapper = mountWithApp(DashboardServerLoad, {
      props: { servers: [server(1, 'alpha', 10, 100), server(2, 'beta', 90, 100), server(3, 'gamma', null, 100)], loading: false },
    })
    const html = wrapper.html()

    expect(wrapper.find('[data-testid="dashboard-server-load-3"]').exists()).toBe(false)
    expect(html.indexOf('beta')).toBeLessThan(html.indexOf('alpha'))
  })

  it('shows the empty state without measurable servers', () => {
    const wrapper = mountWithApp(DashboardServerLoad, { props: { servers: [server(3, 'gamma', null, null)], loading: false } })

    expect(wrapper.find('[data-testid="dashboard-server-load-empty"]').exists()).toBe(true)
  })

  it('keeps the title and shows a placeholder bar while the servers load', () => {
    const wrapper = mountWithApp(DashboardServerLoad, { props: { servers: [], loading: true } })

    expect(wrapper.text()).toContain('Server capacity')
    expect(wrapper.find('[data-testid="ui-progress-skeleton"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="dashboard-server-load-empty"]').exists()).toBe(false)
  })
})
