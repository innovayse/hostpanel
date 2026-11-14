/**
 * Unit tests for PluginProcessingDialog: the running state that cannot be dismissed, the done state with the
 * restart note, the refusal sentence, and the Close button.
 */
import { describe, expect, it } from 'vitest'
import { mount } from '@vue/test-utils'
import PluginProcessingDialog from '@/components/plugins/PluginProcessingDialog.vue'
import { PluginProcessingStep } from '@/types/plugin'
import { createTestI18n } from '../../helpers/i18n'

/**
 * Mounts the dialog with the teleport inlined.
 *
 * @param overrides - Props to change.
 * @returns The wrapper.
 */
const mountDialog = (overrides: Record<string, unknown> = {}) =>
  mount(PluginProcessingDialog, {
    props: {
      open: true,
      name: 'Example Pay',
      step: PluginProcessingStep.Installing,
      running: true,
      failed: false,
      message: null,
      restartRequired: false,
      ...overrides,
    },
    global: { plugins: [createTestI18n()], stubs: { teleport: true } },
  })

describe('PluginProcessingDialog', () => {
  it('shows the plugin name and a loader while the job runs, with nothing to dismiss it', async () => {
    const wrapper = mountDialog()

    expect(wrapper.get('h2').text()).toBe('Example Pay')
    expect(wrapper.find('[data-testid="plugin-processing-running"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="dialog-close"]').exists()).toBe(false)
    expect(wrapper.find('[data-testid="plugin-processing-close"]').exists()).toBe(false)
    await wrapper.get('[role="dialog"]').trigger('keydown', { key: 'Escape' })
    await wrapper.get('[data-testid="dialog-backdrop"]').trigger('click')
    expect(wrapper.emitted('close')).toBeUndefined()
  })

  it('ends with the restart note and a Close button after a success that needs a restart', async () => {
    const wrapper = mountDialog({ running: false, restartRequired: true })

    expect(wrapper.find('[data-testid="plugin-processing-restart"]').exists()).toBe(true)
    await wrapper.get('[data-testid="plugin-processing-close"]').trigger('click')
    expect(wrapper.emitted('close')).toHaveLength(1)
  })

  it('shows no restart note after a success that needs none', () => {
    const wrapper = mountDialog({ running: false })

    expect(wrapper.find('[data-testid="plugin-processing-restart"]').exists()).toBe(false)
  })

  it('shows the API sentence and no restart note after a refusal', () => {
    const wrapper = mountDialog({ running: false, failed: true, message: 'Plugin is in use.', restartRequired: true })

    expect(wrapper.get('[data-testid="plugin-processing-message"]').text()).toContain('Plugin is in use.')
    expect(wrapper.find('[data-testid="plugin-processing-restart"]').exists()).toBe(false)
  })
})
