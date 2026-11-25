/**
 * Unit tests for PluginCardActions: one control per action, Install and Update as the same primary button, the busy state,
 * the placeholder, the pressed action reported, and the test ids from the default rule or the screen's prefixes.
 */
import { describe, expect, it } from 'vitest'
import { mount } from '@vue/test-utils'
import PluginCardActions from '@/components/plugins/PluginCardActions.vue'
import { PluginCardAction } from '@/types/plugin'
import { createTestI18n } from '../../helpers/i18n'

/**
 * Mounts the footer actions.
 *
 * @param props - Props to set on top of the plugin id.
 * @returns The wrapper.
 */
const mountActions = (props: Record<string, unknown> = {}) =>
  mount(PluginCardActions, { props: { pluginId: 'example-pay', actions: [], ...props }, global: { plugins: [createTestI18n()] } })

describe('PluginCardActions', () => {
  it('draws one control per action, in order, with the default test ids', () => {
    const wrapper = mountActions({ actions: [PluginCardAction.Update, PluginCardAction.Settings], version: '2.0.0' })

    expect(wrapper.get('[data-testid="plugin-card-action-update-example-pay"]').text()).toBe('Update to 2.0.0')
    expect(wrapper.get('[data-testid="plugin-card-action-update-example-pay"]').element.nextElementSibling?.getAttribute('data-testid')).toBe('plugin-card-action-settings-example-pay')
    expect(wrapper.find('[data-testid="plugin-card-action-install-example-pay"]').exists()).toBe(false)
  })

  it('draws Install and Update as the same primary button', () => {
    const install = mountActions({ actions: [PluginCardAction.Install] }).get('[data-testid="plugin-card-action-install-example-pay"]')
    const update = mountActions({ actions: [PluginCardAction.Update], version: '2.0.0' }).get('[data-testid="plugin-card-action-update-example-pay"]')

    expect(install.text()).toBe('Install')
    expect(update.classes()).toEqual(install.classes())
  })

  it('draws Details, the placeholder and the gear', () => {
    const wrapper = mountActions({ actions: [PluginCardAction.Details, PluginCardAction.Checking, PluginCardAction.Settings] })

    expect(wrapper.get('[data-testid="plugin-card-action-details-example-pay"]').text()).toBe('Details')
    expect(wrapper.get('[data-testid="plugin-card-action-checking-example-pay"]').attributes('aria-hidden')).toBe('true')
    expect(wrapper.find('[data-testid="plugin-card-action-settings-example-pay"]').exists()).toBe(true)
  })

  it('shows the busy state on Install and Update and cannot be pressed again', async () => {
    const wrapper = mountActions({ actions: [PluginCardAction.Install], busy: true })
    const button = wrapper.get('[data-testid="plugin-card-action-install-example-pay"]')

    expect(button.attributes('aria-busy')).toBe('true')
    expect(button.attributes('disabled')).toBeDefined()
    await button.trigger('click')

    expect(wrapper.emitted('select')).toBeUndefined()
  })

  it('reports the pressed action', async () => {
    const wrapper = mountActions({ actions: [PluginCardAction.Install, PluginCardAction.Details, PluginCardAction.Settings] })

    await wrapper.get('[data-testid="plugin-card-action-install-example-pay"]').trigger('click')
    await wrapper.get('[data-testid="plugin-card-action-details-example-pay"]').trigger('click')
    await wrapper.get('[data-testid="plugin-card-action-settings-example-pay"]').trigger('click')

    expect(wrapper.emitted('select')).toEqual([[PluginCardAction.Install], [PluginCardAction.Details], [PluginCardAction.Settings]])
  })

  it('takes the test id prefix the screen names and falls back to the default for the rest', () => {
    const wrapper = mountActions({
      actions: [PluginCardAction.Update, PluginCardAction.Settings],
      version: '2.0.0',
      testidPrefixes: { [PluginCardAction.Update]: 'registry-install' },
    })

    expect(wrapper.find('[data-testid="registry-install-example-pay"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="plugin-card-action-settings-example-pay"]').exists()).toBe(true)
  })
})
