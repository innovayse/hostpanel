/**
 * Mount tests for ServerCard: the module, name, flags, address, load, facts and last connection state of a server, the
 * running test and its outcome, the row actions, and the same layout drawn with placeholder bars while there is no
 * server yet.
 */
import { describe, expect, it } from 'vitest'
import ServerCard from '@/components/servers/ServerCard.vue'
import type { UiMenuAction } from '@/utils/ui/menu'
import { serverDto } from '../../helpers/serverFixtures'
import { mountWithApp } from '../../helpers/mount'
import { formatDateTime } from '@/lib/format'
import { Icon } from '@/lib/icons'

/** Two row actions, so the card draws its menu trigger. */
const actions: UiMenuAction[] = [{ label: 'Edit', icon: Icon.Edit, onSelect: () => undefined }, { label: 'Delete', icon: Icon.Trash, danger: true, onSelect: () => undefined }]

describe('ServerCard', () => {
  it('shows the module, name, address, load and state of a server', () => {
    const wrapper = mountWithApp(ServerCard, { props: { server: serverDto({ lastTestedAt: '2026-01-02T10:00:00Z' }), actions } })

    expect(wrapper.get('[data-testid="server-row-1"]').text()).toContain('host1')
    expect(wrapper.find('[data-testid="servers-module-CPanel"]').exists()).toBe(true)
    expect(wrapper.text()).toContain('203.0.113.10')
    expect(wrapper.get('[data-testid="ui-progress-value"]').text()).toBe('12 / 100')
    expect(wrapper.get('[data-testid="server-state-1"]').text()).toBe('Online')
    expect(wrapper.find('[data-testid="server-actions-1"]').exists()).toBe(true)
  })

  it('draws the facts of the server in a compact grid', () => {
    const wrapper = mountWithApp(ServerCard, { props: { server: serverDto({ serverGroupName: 'Shared' }) } })

    expect(wrapper.get('[data-testid="server-facts-1"]').text()).toContain('Shared')
    expect(wrapper.get('[data-testid="server-fact-services"]').text()).toContain('3')
    expect(wrapper.get('[data-testid="server-fact-revenue"]').text()).toContain('45.00 USD / month')
    expect(wrapper.get('[data-testid="server-fact-connection"]').text()).toContain('API over HTTPS')
    expect(wrapper.find('[data-testid="server-fact-datacenter"]').exists()).toBe(false)
  })

  it('names the default badge and explains it in a tooltip', () => {
    const wrapper = mountWithApp(ServerCard, { props: { server: serverDto() } })

    expect(wrapper.get('[data-testid="server-default-badge"]').text()).toBe('Default')
    expect(wrapper.find('[role="tooltip"]').text()).toBe('The default server for cPanel / WHM.')
  })

  it('shows the last check as relative time with the full date in a tooltip', () => {
    const wrapper = mountWithApp(ServerCard, { props: { server: serverDto({ lastTestedAt: '2026-01-02T10:00:00Z' }) } })

    const checked = wrapper.get('[data-testid="server-last-checked-1"]')
    expect(checked.text()).toMatch(/^Checked .*ago$/)
    expect(wrapper.findAll('[role="tooltip"]').map((tip) => tip.text())).toContain(formatDateTime('2026-01-02T10:00:00Z'))
  })

  it('flags a default and a disabled server', () => {
    const wrapper = mountWithApp(ServerCard, { props: { server: serverDto({ limits: { maxAccounts: 100, isDefault: true, isDisabled: true } }) } })

    expect(wrapper.text().toLowerCase()).toContain('default')
    expect(wrapper.text().toLowerCase()).toContain('disabled')
  })

  it('shows the accounts as plain text when the server has no capacity limit', () => {
    const wrapper = mountWithApp(ServerCard, { props: { server: serverDto({ limits: { maxAccounts: null, isDefault: false, isDisabled: false } }) } })

    expect(wrapper.find('[data-testid="ui-progress-value"]').exists()).toBe(false)
    expect(wrapper.text()).toContain('12')
  })

  it('shows the running test in place of the state, as placeholder bars announced as busy', () => {
    const wrapper = mountWithApp(ServerCard, { props: { server: serverDto(), testing: true } })

    const testing = wrapper.get('[data-testid="server-testing-1"]')
    expect(testing.attributes('role')).toBe('status')
    expect(testing.attributes('aria-busy')).toBe('true')
    expect(testing.findAll('[data-testid="ui-skeleton-bar"]')).toHaveLength(2)
    expect(wrapper.find('[data-testid="server-state-1"]').exists()).toBe(false)
  })

  it('shows the outcome of the last test in place of the state', () => {
    const wrapper = mountWithApp(ServerCard, { props: { server: serverDto(), testOutcome: { ok: false, message: 'Refused.' } } })

    expect(wrapper.get('[data-testid="server-test-result-1"]').text()).toBe('Refused.')
    expect(wrapper.find('[data-testid="server-state-1"]').exists()).toBe(false)
  })

  it('draws the same layout with bars and no actions while there is no server', () => {
    const wrapper = mountWithApp(ServerCard, { props: { actions } })

    expect(wrapper.attributes('aria-busy')).toBe('true')
    expect(wrapper.find('[data-testid="server-card-loading"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="ui-progress-skeleton"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="server-facts-loading"]').exists()).toBe(true)
    expect(wrapper.findAll('[data-testid="ui-skeleton-bar"]').length).toBeGreaterThanOrEqual(16)
    expect(wrapper.find('[data-testid="server-actions-1"]').exists()).toBe(false)
  })
})
