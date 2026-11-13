/**
 * Unit tests for PluginKeptData: one row per removed plugin and the purge event.
 */
import { describe, expect, it } from 'vitest'
import { mount } from '@vue/test-utils'
import PluginKeptData from '@/components/plugins/PluginKeptData.vue'
import { createTestI18n } from '../../helpers/i18n'

/**
 * Mounts the list.
 *
 * @param overrides - Props to change.
 * @returns The wrapper.
 */
const mountList = (overrides: Record<string, unknown> = {}) =>
  mount(PluginKeptData, { props: { ids: ['example-pay', 'acme'], purgingId: null, ...overrides }, global: { plugins: [createTestI18n()] } })

describe('PluginKeptData', () => {
  it('lists every removed plugin with a purge button', () => {
    const wrapper = mountList()

    expect(wrapper.get('[data-testid="plugin-kept-example-pay"]').text()).toContain('example-pay')
    expect(wrapper.get('[data-testid="plugin-purge-acme"]').text()).toBe('Purge data')
  })

  it('emits purge with the plugin id', async () => {
    const wrapper = mountList()

    await wrapper.get('[data-testid="plugin-purge-acme"]').trigger('click')

    expect(wrapper.emitted('purge')).toEqual([['acme']])
  })

  it('disables the buttons while one purge runs, keeping the label', () => {
    const wrapper = mountList({ purgingId: 'example-pay' })

    expect(wrapper.get('[data-testid="plugin-purge-example-pay"]').text()).toContain('Purge data')
    expect(wrapper.get('[data-testid="plugin-purge-acme"]').attributes('disabled')).toBeDefined()
  })
})
