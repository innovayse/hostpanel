/**
 * Unit tests for PluginDetailsHeader: the name, author and badges read from the installed plugin, the registry's
 * additions (verified, price, update offer) that disappear without registry details, and the update and uninstall
 * events (no uninstall for a built-in plugin).
 */
import { describe, expect, it } from 'vitest'
import { mount } from '@vue/test-utils'
import PluginDetailsHeader from '@/components/plugins/details/PluginDetailsHeader.vue'
import { PluginPricing } from '@/types/plugin'
import { createTestI18n } from '../../../helpers/i18n'
import { makeDetails, makeInstalled } from '../../../helpers/pluginDetails'

/** What a test changes: fields of the installed row, of the registry details (null for none) and the busy flag. */
interface HeaderOptions {
  /** Fields of the installed plugin to change. */
  plugin?: Parameters<typeof makeInstalled>[0]
  /** Fields of the registry details to change, or null for no registry details. */
  registry?: Parameters<typeof makeDetails>[0] | null
  /** Whether an action is in flight. */
  busy?: boolean
}

/**
 * Mounts the header.
 *
 * @param options - What to change from the default installed plugin with registry details.
 * @returns The wrapper.
 */
const mountHeader = (options: HeaderOptions = {}) =>
  mount(PluginDetailsHeader, {
    props: {
      plugin: makeInstalled(options.plugin),
      registry: options.registry === null ? null : makeDetails(options.registry),
      busy: options.busy ?? false,
    },
    global: { plugins: [createTestI18n()] },
  })

describe('PluginDetailsHeader', () => {
  it('draws the card with bars instead of the plugin while the list loads', () => {
    const wrapper = mount(PluginDetailsHeader, {
      props: { plugin: null, registry: null, busy: false },
      global: { plugins: [createTestI18n()] },
    })

    expect(wrapper.find('[data-testid="plugin-details-loading"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="plugin-details-name"]').exists()).toBe(false)
    expect(wrapper.find('[data-testid="plugin-details-update"]').exists()).toBe(false)
  })

  it('shows the name, author, description, verified and price badges', () => {
    const wrapper = mountHeader()

    expect(wrapper.get('[data-testid="plugin-details-name"]').text()).toBe('Example Pay')
    expect(wrapper.get('[data-testid="plugin-details-author"]').text()).toContain('Hostpanel')
    expect(wrapper.get('[data-testid="plugin-details-description"]').text()).toBe('Card payments')
    expect(wrapper.find('[data-testid="plugin-details-verified"]').exists()).toBe(true)
    expect(wrapper.get('[data-testid="plugin-details-pricing"]').text()).toBe('Free')
  })

  it('leaves out the verified badge for an unverified publisher and names a paid price', () => {
    const wrapper = mountHeader({ registry: { verified: false, pricing: PluginPricing.Paid } })

    expect(wrapper.find('[data-testid="plugin-details-verified"]').exists()).toBe(false)
    expect(wrapper.get('[data-testid="plugin-details-pricing"]').text()).toBe('Paid')
  })

  it('shows the installed plugin alone, without badges or actions of the registry, when there are no registry details', () => {
    const wrapper = mountHeader({ registry: null })

    expect(wrapper.get('[data-testid="plugin-details-name"]').text()).toBe('Example Pay')
    expect(wrapper.get('[data-testid="plugin-details-installed"]').text()).toContain('2.0.0')
    expect(wrapper.find('[data-testid="plugin-details-verified"]').exists()).toBe(false)
    expect(wrapper.find('[data-testid="plugin-details-pricing"]').exists()).toBe(false)
    expect(wrapper.find('[data-testid="plugin-details-update-available"]').exists()).toBe(false)
    expect(wrapper.find('[data-testid="plugin-details-update"]').exists()).toBe(false)
    expect(wrapper.find('[data-testid="plugin-details-uninstall"]').exists()).toBe(true)
  })

  it('shows the installed version, the staged version and the update on offer', () => {
    const wrapper = mountHeader({ plugin: { stagedVersion: '3.0.0' } })

    expect(wrapper.get('[data-testid="plugin-details-installed"]').text()).toContain('2.0.0')
    expect(wrapper.get('[data-testid="plugin-details-staged"]').text()).toContain('3.0.0')
    expect(wrapper.get('[data-testid="plugin-details-update-available"]').text()).toContain('3.0.0')
  })

  it('offers no update button when the newest version is already staged, but still names the update', () => {
    const wrapper = mountHeader({ plugin: { stagedVersion: '3.0.0' } })

    expect(wrapper.find('[data-testid="plugin-details-update"]').exists()).toBe(false)
    expect(wrapper.find('[data-testid="plugin-details-update-available"]').exists()).toBe(true)
  })

  it('offers the update button when a different version than the newest is staged', () => {
    const wrapper = mountHeader({ plugin: { stagedVersion: '2.5.0' } })

    expect(wrapper.find('[data-testid="plugin-details-update"]').exists()).toBe(true)
  })

  it('offers no update when the registry has no newer version', () => {
    const wrapper = mountHeader({ registry: { updateAvailable: false, latestVersion: '2.0.0' } })

    expect(wrapper.find('[data-testid="plugin-details-update"]').exists()).toBe(false)
  })

  it('offers no uninstall for a built-in plugin', () => {
    const wrapper = mountHeader({ plugin: { isBuiltIn: true } })

    expect(wrapper.find('[data-testid="plugin-details-uninstall"]').exists()).toBe(false)
  })

  it('emits update and uninstall from their buttons', async () => {
    const wrapper = mountHeader()

    await wrapper.get('[data-testid="plugin-details-update"]').trigger('click')
    await wrapper.get('[data-testid="plugin-details-uninstall"]').trigger('click')

    expect(wrapper.emitted('update')).toHaveLength(1)
    expect(wrapper.emitted('uninstall')).toHaveLength(1)
  })

  it('blocks the buttons while an action runs', () => {
    const wrapper = mountHeader({ busy: true })

    expect(wrapper.get('[data-testid="plugin-details-update"]').attributes('disabled')).toBeDefined()
    expect(wrapper.get('[data-testid="plugin-details-uninstall"]').attributes('disabled')).toBeDefined()
  })
})
