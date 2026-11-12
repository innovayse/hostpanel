/**
 * Tests for PluginInstalledGrid: an installed plugin as the shared tile in a three column grid, its
 * status, staged, update, built-in and capability badges, the Update, settings and Remove actions
 * with their events and busy states, the refused state (reason, id, rule, only Remove), and the
 * skeleton tiles while loading.
 */
import { describe, expect, it } from 'vitest'
import { mount } from '@vue/test-utils'
import PluginInstalledGrid from '@/components/plugins/PluginInstalledGrid.vue'
import { PluginCapability, PluginStatus } from '@/types/plugin'
import type { PluginListItemDto } from '@/types/plugin'
import { createTestI18n } from '../../helpers/i18n'

/** An installed plugin that is already running. */
const plugin: PluginListItemDto = {
  id: 'example-pay',
  name: 'Example Pay',
  version: '1.2.0',
  author: 'Hostpanel',
  description: 'Card payments',
  type: 'payment',
  category: 'Payments',
  color: '#123456',
  iconUrl: null,
  isLoaded: true,
  isBuiltIn: false,
  status: PluginStatus.Loaded,
  refusedRule: null,
  refusedDetail: null,
  stagedVersion: null,
  capabilities: [],
}

/** A plugin the host skipped because it broke a manifest rule. */
const refusedPlugin: PluginListItemDto = {
  ...plugin,
  isLoaded: false,
  status: PluginStatus.Refused,
  refusedRule: 'PermissionUnknown',
  refusedDetail: 'Billing.Everything',
}

/**
 * Mounts the grid.
 *
 * @param overrides - Props to change.
 * @returns The wrapper.
 */
const mountGrid = (overrides: Record<string, unknown> = {}) =>
  mount(PluginInstalledGrid, { props: { plugins: [plugin], ...overrides }, global: { plugins: [createTestI18n()] } })

describe('PluginInstalledGrid', () => {
  it('shows each plugin as a shared tile with its meta line and version', () => {
    const wrapper = mountGrid({ plugins: [plugin, { ...plugin, id: 'example-host', name: 'Example Host' }] })

    expect(wrapper.findAll('[data-testid^="plugin-card-"]')).toHaveLength(2)
    expect(wrapper.get('[data-testid="plugin-card-example-pay"]').text()).toContain('Example Pay')
    expect(wrapper.get('[data-testid="plugin-tile-meta-example-pay"]').text()).toBe('Hostpanel · Payments')
    expect(wrapper.get('[data-testid="plugin-tile-version-example-pay"]').text()).toBe('v1.2.0')
  })

  it('lists the tiles in one column, then two, then three as the screen widens', () => {
    expect(mountGrid().get('[data-testid="plugin-installed-grid"]').classes()).toEqual(
      expect.arrayContaining(['grid-cols-1', 'md:grid-cols-2', 'lg:grid-cols-3']),
    )
  })

  it('marks a running plugin active and one not loaded yet as pending restart', () => {
    expect(mountGrid().get('[data-testid="plugin-tile-badge-example-pay-status"]').text()).toBe('Active')
    const pending = mountGrid({ plugins: [{ ...plugin, isLoaded: false }] })

    expect(pending.get('[data-testid="plugin-tile-badge-example-pay-status"]').text()).toBe('Pending restart')
  })

  it('marks a built-in plugin and offers no Remove button', () => {
    const wrapper = mountGrid({ plugins: [{ ...plugin, isBuiltIn: true }] })

    expect(wrapper.get('[data-testid="plugin-tile-badge-example-pay-builtin"]').text()).toBe('Built-in')
    expect(wrapper.find('[data-testid="plugin-remove-example-pay"]').exists()).toBe(false)
  })

  it('shows a badge per declared capability and none otherwise', () => {
    const wrapper = mountGrid({ plugins: [{ ...plugin, capabilities: [PluginCapability.Endpoints] }] })

    expect(wrapper.get('[data-testid="plugin-tile-badge-example-pay-capability-endpoints"]').text()).toBe('Serves pages')
    expect(mountGrid().find('[data-testid^="plugin-tile-badge-example-pay-capability"]').exists()).toBe(false)
  })

  it('shows the staged version of a pending upgrade and nothing when none waits', () => {
    const wrapper = mountGrid({ plugins: [{ ...plugin, stagedVersion: '1.3.0' }] })

    expect(wrapper.get('[data-testid="plugin-tile-badge-example-pay-staged"]').text()).toContain('Version 1.3.0 staged')
    expect(mountGrid().find('[data-testid="plugin-tile-badge-example-pay-staged"]').exists()).toBe(false)
  })

  it('shows an update badge and an Update button with the newer version, and emits the id and version', async () => {
    const wrapper = mountGrid({ updateVersions: { 'example-pay': '2.0.0' } })
    const button = wrapper.get('[data-testid="plugin-update-button-example-pay"]')

    expect(wrapper.get('[data-testid="plugin-tile-badge-example-pay-update"]').text()).toBe('Update available: 2.0.0')
    expect(button.text()).toBe('Update to 2.0.0')
    await button.trigger('click')

    expect(wrapper.emitted('update')).toEqual([['example-pay', '2.0.0']])
  })

  it('shows no update badge or Update button without a newer version', () => {
    const wrapper = mountGrid()

    expect(wrapper.find('[data-testid="plugin-tile-badge-example-pay-update"]').exists()).toBe(false)
    expect(wrapper.find('[data-testid="plugin-update-button-example-pay"]').exists()).toBe(false)
  })

  it('offers no release notes toggles on the tile', () => {
    const wrapper = mountGrid({ updateVersions: { 'example-pay': '2.0.0' } })

    expect(wrapper.find('[data-testid^="plugin-notes"]').exists()).toBe(false)
    expect(wrapper.find('[data-testid^="plugin-update-notes"]').exists()).toBe(false)
  })

  it('disables Update and Remove while the update runs, and the pair of the removing plugin only', () => {
    const updating = mountGrid({ updateVersions: { 'example-pay': '2.0.0' }, updatingIds: ['example-pay'] })

    expect(updating.get('[data-testid="plugin-update-button-example-pay"]').attributes('disabled')).toBeDefined()
    expect(updating.get('[data-testid="plugin-update-button-example-pay"]').text()).toBe('Installing…')
    expect(updating.get('[data-testid="plugin-remove-example-pay"]').attributes('disabled')).toBeDefined()

    const removing = mountGrid({ plugins: [plugin, { ...plugin, id: 'example-host' }], removingId: 'example-pay' })

    expect(removing.get('[data-testid="plugin-remove-example-pay"]').text()).toBe('Removing…')
    expect(removing.get('[data-testid="plugin-remove-example-pay"]').attributes('disabled')).toBeDefined()
    expect(removing.get('[data-testid="plugin-remove-example-host"]').attributes('disabled')).toBeUndefined()
  })

  it('emits remove with the plugin id', async () => {
    const wrapper = mountGrid()

    await wrapper.get('[data-testid="plugin-remove-example-pay"]').trigger('click')

    expect(wrapper.emitted('remove')).toEqual([['example-pay']])
  })

  it('has a settings button with an accessible name that emits settings with the plugin id', async () => {
    const wrapper = mountGrid()
    const gear = wrapper.get('[data-testid="plugin-settings-example-pay"]')

    expect(gear.attributes('aria-label')).toBe('Open settings')
    expect(gear.find('svg').exists()).toBe(true)
    await gear.trigger('click')

    expect(wrapper.emitted('settings')).toEqual([['example-pay']])
  })

  it('opens the plugin page from the name too', async () => {
    const wrapper = mountGrid()

    await wrapper.get('[data-testid="plugin-tile-open-example-pay"]').trigger('click')

    expect(wrapper.emitted('settings')).toEqual([['example-pay']])
  })

  it('shows a refused plugin with its id, rule, localised reason and detail, never as active', () => {
    const wrapper = mountGrid({ plugins: [refusedPlugin] })

    expect(wrapper.get('[data-testid="plugin-tile-badge-example-pay-refused"]').text()).toBe('Refused')
    const refusal = wrapper.get('[data-testid="plugin-refusal-example-pay"]').text()
    expect(refusal).toContain('A client entry names a permission that does not exist.')
    expect(refusal).toContain('example-pay')
    expect(refusal).toContain('PermissionUnknown')
    expect(refusal).toContain('Billing.Everything')
    expect(wrapper.text()).not.toContain('Active')
    expect(wrapper.text()).not.toContain('Pending restart')
  })

  it('lets a refused plugin only be removed, with no settings button and no page to open', async () => {
    const wrapper = mountGrid({ plugins: [refusedPlugin] })

    expect(wrapper.find('[data-testid="plugin-settings-example-pay"]').exists()).toBe(false)
    expect(wrapper.find('[data-testid="plugin-tile-open-example-pay"]').exists()).toBe(false)
    await wrapper.get('[data-testid="plugin-remove-example-pay"]').trigger('click')

    expect(wrapper.emitted('remove')).toEqual([['example-pay']])
  })

  it('shows no refusal block for a loaded plugin', () => {
    expect(mountGrid().find('[data-testid="plugin-refusal-example-pay"]').exists()).toBe(false)
  })

  it('draws the placeholder tiles instead of the plugins while loading', () => {
    const wrapper = mountGrid({ loading: true })

    expect(wrapper.findAll('[data-testid="plugin-tile-skeleton"]')).toHaveLength(3)
    expect(wrapper.find('[data-testid="plugin-card-example-pay"]').exists()).toBe(false)
    expect(wrapper.get('[data-testid="plugin-installed-grid"]').attributes('aria-busy')).toBe('true')
  })

  it('draws as many placeholder tiles as asked', () => {
    expect(mountGrid({ loading: true, skeletonCount: 5 }).findAll('[data-testid="plugin-tile-skeleton"]')).toHaveLength(5)
  })
})
