/**
 * Unit tests for PluginDetailsHeader: the name, publisher and badges, the state on this server, and
 * the update and uninstall events.
 */
import { describe, expect, it } from 'vitest'
import { mount } from '@vue/test-utils'
import PluginDetailsHeader from '@/components/plugins/details/PluginDetailsHeader.vue'
import { PluginPricing } from '@/types/plugin'
import { createTestI18n } from '../../../helpers/i18n'
import { makeDetails } from '../../../helpers/pluginDetails'

/**
 * Mounts the header.
 *
 * @param details - Fields of the details to change.
 * @param busy - Whether an action is in flight.
 * @returns The wrapper.
 */
const mountHeader = (details: Parameters<typeof makeDetails>[0] = {}, busy = false) =>
  mount(PluginDetailsHeader, {
    props: { details: makeDetails(details), busy },
    global: { plugins: [createTestI18n()] },
  })

describe('PluginDetailsHeader', () => {
  it('draws the card with bars instead of the details while they load', () => {
    const wrapper = mount(PluginDetailsHeader, {
      props: { details: null, busy: false },
      global: { plugins: [createTestI18n()] },
    })

    expect(wrapper.find('[data-testid="plugin-details-loading"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="plugin-details-name"]').exists()).toBe(false)
    expect(wrapper.find('[data-testid="plugin-details-update"]').exists()).toBe(false)
  })

  it('shows the name, publisher, description, verified and price badges', () => {
    const wrapper = mountHeader()

    expect(wrapper.get('[data-testid="plugin-details-name"]').text()).toBe('Example Pay')
    expect(wrapper.get('[data-testid="plugin-details-author"]').text()).toContain('Hostpanel')
    expect(wrapper.get('[data-testid="plugin-details-description"]').text()).toBe('Card payments')
    expect(wrapper.find('[data-testid="plugin-details-verified"]').exists()).toBe(true)
    expect(wrapper.get('[data-testid="plugin-details-pricing"]').text()).toBe('Free')
  })

  it('leaves out the verified badge for an unverified publisher and names a paid price', () => {
    const wrapper = mountHeader({ verified: false, pricing: PluginPricing.Paid })

    expect(wrapper.find('[data-testid="plugin-details-verified"]').exists()).toBe(false)
    expect(wrapper.get('[data-testid="plugin-details-pricing"]').text()).toBe('Paid')
  })

  it('shows the installed version, the staged version and the update on offer', () => {
    const wrapper = mountHeader({ stagedVersion: '3.0.0' })

    expect(wrapper.get('[data-testid="plugin-details-installed"]').text()).toContain('2.0.0')
    expect(wrapper.get('[data-testid="plugin-details-staged"]').text()).toContain('3.0.0')
    expect(wrapper.get('[data-testid="plugin-details-update-available"]').text()).toContain('3.0.0')
  })

  it('offers no update button when the newest version is already staged, but still names the update', () => {
    const wrapper = mountHeader({ stagedVersion: '3.0.0' })

    expect(wrapper.find('[data-testid="plugin-details-update"]').exists()).toBe(false)
    expect(wrapper.find('[data-testid="plugin-details-update-available"]').exists()).toBe(true)
  })

  it('offers the update button when a different version than the newest is staged', () => {
    const wrapper = mountHeader({ stagedVersion: '2.5.0' })

    expect(wrapper.find('[data-testid="plugin-details-update"]').exists()).toBe(true)
  })

  it('offers no update and no uninstall for a plugin that is not installed', () => {
    const wrapper = mountHeader({ installed: false, installedVersion: null, updateAvailable: false })

    expect(wrapper.find('[data-testid="plugin-details-not-installed"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="plugin-details-update"]').exists()).toBe(false)
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
    const wrapper = mountHeader({}, true)

    expect(wrapper.get('[data-testid="plugin-details-update"]').attributes('disabled')).toBeDefined()
    expect(wrapper.get('[data-testid="plugin-details-uninstall"]').attributes('disabled')).toBeDefined()
  })
})
