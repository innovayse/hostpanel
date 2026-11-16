/**
 * Mount tests for ServerCard: the module, name, flags, address, load and last connection state of a server, the
 * running test and its outcome, the row actions, and the same layout drawn with placeholder bars while there is no
 * server yet.
 */
import { describe, expect, it } from 'vitest'
import ServerCard from '@/components/servers/ServerCard.vue'
import type { UiMenuAction } from '@/utils/ui/menu'
import { serverDto } from '../../helpers/serverFixtures'
import { mountWithApp } from '../../helpers/mount'

/** Two row actions, so the card draws its menu trigger. */
const actions: UiMenuAction[] = [{ label: 'Edit', onSelect: () => undefined }, { label: 'Delete', danger: true, onSelect: () => undefined }]

describe('ServerCard', () => {
  it('shows the module, name, address, load and state of a server', () => {
    const wrapper = mountWithApp(ServerCard, { props: { server: serverDto({ lastTestedAt: '2026-01-02T10:00:00Z' }), actions } })

    expect(wrapper.get('[data-testid="server-row-1"]').text()).toContain('host1')
    expect(wrapper.find('[data-testid="servers-module-Cwp7"]').exists()).toBe(true)
    expect(wrapper.text()).toContain('203.0.113.10')
    expect(wrapper.get('[data-testid="ui-progress-value"]').text()).toBe('12 / 100')
    expect(wrapper.get('[data-testid="server-state-1"]').text()).toBe('Online')
    expect(wrapper.find('[data-testid="server-actions-1"]').exists()).toBe(true)
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

  it('shows the running test in place of the state', () => {
    const wrapper = mountWithApp(ServerCard, { props: { server: serverDto(), testing: true } })

    expect(wrapper.find('[data-testid="server-testing-1"]').exists()).toBe(true)
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
    expect(wrapper.findAll('[data-testid="ui-skeleton-bar"]').length).toBeGreaterThanOrEqual(8)
    expect(wrapper.find('[data-testid="server-actions-1"]').exists()).toBe(false)
  })
})
