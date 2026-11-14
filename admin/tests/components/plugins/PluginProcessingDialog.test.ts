/**
 * Unit tests for PluginProcessingDialog: the running state that cannot be dismissed, the done state with the
 * restart note, the refusal sentence, and the Close button that closes the screen's processing state.
 */
import { describe, expect, it } from 'vitest'
import { createPinia, setActivePinia } from 'pinia'
import { ref } from 'vue'
import { mount } from '@vue/test-utils'
import PluginProcessingDialog from '@/components/plugins/PluginProcessingDialog.vue'
import type { PluginProcessing } from '@/composables/usePluginProcessing'
import { usePluginsStore } from '@/stores/plugins'
import { PluginProcessingStep } from '@/types/plugin'
import { createTestI18n } from '../../helpers/i18n'

/**
 * Mounts the dialog over a processing state, with the teleport inlined.
 *
 * @param overrides - Fields of the state to change.
 * @param restartRequired - Whether the plugins store says the server must restart.
 * @returns The wrapper and the number of times the state was asked to close.
 */
const mountDialog = (overrides: { running?: boolean; failed?: boolean; message?: string | null } = {}, restartRequired = false) => {
  const pinia = createPinia()
  setActivePinia(pinia)
  usePluginsStore().requiresRestart = restartRequired
  const closes = { count: 0 }
  const processing: PluginProcessing = {
    open: ref(true),
    name: ref('Example Pay'),
    step: ref(PluginProcessingStep.Installing),
    running: ref(overrides.running ?? true),
    failed: ref(overrides.failed ?? false),
    message: ref(overrides.message ?? null),
    /**
     * Not used by the dialog: it only reads the state.
     *
     * @returns A finished outcome.
     */
    run: async () => ({ ok: true, stagedVersion: null, error: null }),
    /** Counts the close requests. */
    close: () => {
      closes.count += 1
    },
  }
  const wrapper = mount(PluginProcessingDialog, {
    props: { processing },
    global: { plugins: [createTestI18n(), pinia], stubs: { teleport: true } },
  })
  return { wrapper, closes }
}

describe('PluginProcessingDialog', () => {
  it('shows the plugin name and a loader while the job runs, with nothing to dismiss it', async () => {
    const { wrapper, closes } = mountDialog()

    expect(wrapper.get('h2').text()).toBe('Example Pay')
    expect(wrapper.find('[data-testid="plugin-processing-running"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="dialog-close"]').exists()).toBe(false)
    expect(wrapper.find('[data-testid="plugin-processing-close"]').exists()).toBe(false)
    await wrapper.get('[role="dialog"]').trigger('keydown', { key: 'Escape' })
    await wrapper.get('[data-testid="dialog-backdrop"]').trigger('click')
    expect(closes.count).toBe(0)
  })

  it('ends with the restart note and a Close button after a success that needs a restart', async () => {
    const { wrapper, closes } = mountDialog({ running: false }, true)

    expect(wrapper.find('[data-testid="plugin-processing-restart"]').exists()).toBe(true)
    await wrapper.get('[data-testid="plugin-processing-close"]').trigger('click')
    expect(closes.count).toBe(1)
  })

  it('shows no restart note after a success that needs none', () => {
    const { wrapper } = mountDialog({ running: false })

    expect(wrapper.find('[data-testid="plugin-processing-restart"]').exists()).toBe(false)
  })

  it('shows the API sentence and no restart note after a refusal', () => {
    const { wrapper } = mountDialog({ running: false, failed: true, message: 'Plugin is in use.' }, true)

    expect(wrapper.get('[data-testid="plugin-processing-message"]').text()).toContain('Plugin is in use.')
    expect(wrapper.find('[data-testid="plugin-processing-restart"]').exists()).toBe(false)
  })
})
