/**
 * Unit tests for RestartBanner: the warning text and the restart event.
 */
import { describe, expect, it } from 'vitest'
import { mount } from '@vue/test-utils'
import RestartBanner from '@/components/plugins/RestartBanner.vue'
import { createTestI18n } from '../../helpers/i18n'

describe('RestartBanner', () => {
  it('asks for a restart and emits restart on click', async () => {
    const wrapper = mount(RestartBanner, { props: { restarting: false }, global: { plugins: [createTestI18n()] } })

    expect(wrapper.text()).toContain('Restart required')
    await wrapper.get('[data-testid="plugins-restart"]').trigger('click')

    expect(wrapper.emitted('restart')).toHaveLength(1)
  })

  it('disables the button while the restart is running', () => {
    const wrapper = mount(RestartBanner, { props: { restarting: true }, global: { plugins: [createTestI18n()] } })

    expect(wrapper.get('[data-testid="plugins-restart"]').attributes('disabled')).toBeDefined()
    expect(wrapper.text()).toContain('Restarting')
  })

  it('spreads text and button across the alert and wraps on a narrow screen', () => {
    const wrapper = mount(RestartBanner, { props: { restarting: false }, global: { plugins: [createTestI18n()] } })
    const row = wrapper.get('[data-testid="ui-alert-body"] > div')

    expect(row.classes()).toEqual(expect.arrayContaining(['flex-row', 'justify-between', 'flex-wrap']))
    expect(row.find('[data-testid="plugins-restart"]').exists()).toBe(true)
  })
})
