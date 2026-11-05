/**
 * Unit tests for PluginCard: name, version and status, the built-in mark, the registry update badge, the capability badges, and the remove event.
 */
import { describe, expect, it } from 'vitest'
import { mount } from '@vue/test-utils'
import PluginCard from '@/components/plugins/PluginCard.vue'
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

/**
 * Mounts the card.
 *
 * @param overrides - Props to change.
 * @returns The wrapper.
 */
const mountCard = (overrides: Record<string, unknown> = {}) =>
  mount(PluginCard, { props: { plugin, removing: false, ...overrides }, global: { plugins: [createTestI18n()] } })

describe('PluginCard', () => {
  it('shows the plugin and its active status', () => {
    const wrapper = mountCard()

    expect(wrapper.text()).toContain('Example Pay')
    expect(wrapper.text()).toContain('v1.2.0')
    expect(wrapper.text()).toContain('Active')
  })

  it('marks a plugin that is not loaded yet as pending restart', () => {
    const wrapper = mountCard({ plugin: { ...plugin, isLoaded: false } })

    expect(wrapper.text()).toContain('Pending restart')
  })

  it('emits remove with the plugin id', async () => {
    const wrapper = mountCard()

    await wrapper.get('[data-testid="plugin-remove-example-pay"]').trigger('click')

    expect(wrapper.emitted('remove')).toEqual([['example-pay']])
  })

  it('marks a built-in plugin and offers no remove button', () => {
    const wrapper = mountCard({ plugin: { ...plugin, isBuiltIn: true } })

    expect(wrapper.get('[data-testid="plugin-builtin-example-pay"]').text()).toBe('Built-in')
    expect(wrapper.find('[data-testid="plugin-remove-example-pay"]').exists()).toBe(false)
  })

  it('shows an update badge with the newer registry version', () => {
    const wrapper = mountCard({ updateVersion: '2.0.0' })

    expect(wrapper.get('[data-testid="plugin-update-example-pay"]').text()).toBe('Update available: 2.0.0')
  })

  it('shows no update badge without a newer version', () => {
    expect(mountCard().find('[data-testid="plugin-update-example-pay"]').exists()).toBe(false)
    expect(mountCard({ updateVersion: null }).find('[data-testid="plugin-update-example-pay"]').exists()).toBe(false)
  })

  it('does not mark an uploaded plugin as built-in', () => {
    const wrapper = mountCard()

    expect(wrapper.find('[data-testid="plugin-builtin-example-pay"]').exists()).toBe(false)
  })

  it('shows a refused plugin with its id, rule, localised reason and detail, never as active', () => {
    const wrapper = mountCard({
      plugin: {
        ...plugin,
        isLoaded: false,
        status: 'Refused',
        refusedRule: 'PermissionUnknown',
        refusedDetail: 'Billing.Everything',
      },
    })

    expect(wrapper.get('[data-testid="plugin-refused-example-pay"]').text()).toBe('Refused')
    const refusal = wrapper.get('[data-testid="plugin-refusal-example-pay"]').text()
    expect(refusal).toContain('A client entry names a permission that does not exist.')
    expect(refusal).toContain('example-pay')
    expect(refusal).toContain('PermissionUnknown')
    expect(refusal).toContain('Billing.Everything')
    expect(wrapper.text()).not.toContain('Active')
    expect(wrapper.text()).not.toContain('Pending restart')
  })

  it('lets a refused plugin be removed', async () => {
    const wrapper = mountCard({ plugin: { ...plugin, isLoaded: false, status: 'Refused', refusedRule: 'RouteDuplicate' } })

    await wrapper.get('[data-testid="plugin-remove-example-pay"]').trigger('click')

    expect(wrapper.emitted('remove')).toEqual([['example-pay']])
  })

  it('shows no refusal block for a loaded plugin', () => {
    const wrapper = mountCard()

    expect(wrapper.find('[data-testid="plugin-refused-example-pay"]').exists()).toBe(false)
    expect(wrapper.find('[data-testid="plugin-refusal-example-pay"]').exists()).toBe(false)
  })

  it('shows the staged version of a pending upgrade', () => {
    const wrapper = mountCard({ plugin: { ...plugin, stagedVersion: '1.3.0' } })

    expect(wrapper.get('[data-testid="plugin-staged-example-pay"]').text()).toContain('Version 1.3.0 staged')
  })

  it('shows no staged mark when no upgrade waits', () => {
    const wrapper = mountCard()

    expect(wrapper.find('[data-testid="plugin-staged-example-pay"]').exists()).toBe(false)
  })

  it('shows a badge per declared capability', () => {
    const wrapper = mountCard({ plugin: { ...plugin, type: 'fulfilment', capabilities: [PluginCapability.Endpoints] } })

    expect(wrapper.get('[data-testid="plugin-capability-example-pay-endpoints"]').text()).toBe('Serves pages')
  })

  it('shows no capability badge when none is declared', () => {
    const wrapper = mountCard()

    expect(wrapper.find('[data-testid^="plugin-capability-example-pay"]').exists()).toBe(false)
  })

  it('disables the remove button while removing', () => {
    const wrapper = mountCard({ removing: true })

    expect(wrapper.get('[data-testid="plugin-remove-example-pay"]').attributes('disabled')).toBeDefined()
    expect(wrapper.text()).toContain('Removing')
  })

  it('shows the plugin icon when it has one and the colour block with the initial when not', () => {
    const withIcon = mountCard({ plugin: { ...plugin, iconUrl: '/api/admin/plugins/example-pay/icon' } })
    const without = mountCard()

    expect(withIcon.get('[data-testid="plugin-avatar-image"]').attributes('src')).toBe('/api/admin/plugins/example-pay/icon')
    expect(without.find('[data-testid="plugin-avatar-image"]').exists()).toBe(false)
    expect(without.get('[data-testid="plugin-avatar-fallback"] text').text()).toBe('E')
  })
})
