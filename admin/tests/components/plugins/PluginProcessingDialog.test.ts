/**
 * Unit tests for PluginProcessingDialog: the running state that cannot be dismissed, the step list and the bar, the done
 * state with the restart note, the refusal sentence, and the Close button that closes the screen's processing state.
 */
import { describe, expect, it } from 'vitest'
import { createPinia, setActivePinia } from 'pinia'
import { defineComponent, h } from 'vue'
import { flushPromises, mount } from '@vue/test-utils'
import PluginProcessingDialog from '@/components/plugins/PluginProcessingDialog.vue'
import { UiStepState } from '@/components/ui/UiStepList.vue'
import { usePluginProcessing } from '@/composables/usePluginProcessing'
import { usePluginsStore } from '@/stores/plugins'
import { PluginProcessingPlan, PluginProcessingStep } from '@/types/plugin'
import type { PluginActionOutcome } from '@/types/plugin'
import { createTestI18n } from '../../helpers/i18n'

/**
 * Mounts the dialog over a real processing state, with the teleport inlined, and starts one job.
 *
 * @param outcome - How the job ends; omitted to keep it running.
 * @param restartRequired - Whether the plugins store says the server must restart.
 * @returns The wrapper.
 */
const mountDialog = async (outcome?: PluginActionOutcome, restartRequired = false) => {
  const pinia = createPinia()
  setActivePinia(pinia)
  usePluginsStore().requiresRestart = restartRequired
  const Host = defineComponent({
    setup: () => {
      const processing = usePluginProcessing()
      void processing.run({
        name: 'Example Pay',
        step: PluginProcessingStep.Installing,
        plan: PluginProcessingPlan.Registry,
        version: '1.2.0',
        work: () => (outcome === undefined ? new Promise<PluginActionOutcome>(() => undefined) : Promise.resolve(outcome)),
      })
      return () => h(PluginProcessingDialog, { processing })
    },
  })
  const wrapper = mount(Host, { global: { plugins: [createTestI18n(), pinia], stubs: { teleport: true } } })
  await flushPromises()
  return wrapper
}

describe('PluginProcessingDialog', () => {
  it('shows the plugin, the version, the bar and the steps while the job runs, with nothing to dismiss it', async () => {
    const wrapper = await mountDialog()

    expect(wrapper.get('h2').text()).toBe('Example Pay')
    expect(wrapper.get('[data-testid="plugin-processing-version"]').text()).toContain('1.2.0')
    expect(wrapper.find('[data-testid="plugin-processing-progress"]').exists()).toBe(true)
    expect(wrapper.get('[data-testid="ui-step-download"]').attributes('data-state')).toBe(UiStepState.Running)
    expect(wrapper.find('[data-testid="plugin-processing-close"]').exists()).toBe(false)
  })

  it('ends with the restart note and a Close button after a success that needs a restart', async () => {
    const wrapper = await mountDialog({ ok: true, stagedVersion: '1.2.0', reloaded: false, restarting: false, error: null }, true)

    expect(wrapper.find('[data-testid="plugin-processing-restart"]').exists()).toBe(true)
    await wrapper.get('[data-testid="plugin-processing-close"]').trigger('click')
    expect(wrapper.find('[data-testid="plugin-processing-body"]').exists()).toBe(false)
  })

  it('shows the green loaded state after a change applied in place', async () => {
    const wrapper = await mountDialog({ ok: true, stagedVersion: null, reloaded: true, restarting: false, error: null })

    expect(wrapper.get('[data-testid="ui-step-loaded"]').attributes('data-state')).toBe(UiStepState.Done)
    expect(wrapper.get('[data-testid="plugin-processing-message"]').text()).toContain('1.2.0')
  })

  it('shows the API sentence and no restart note after a refusal', async () => {
    const wrapper = await mountDialog({ ok: false, stagedVersion: null, reloaded: false, restarting: false, error: 'Plugin is in use.' }, true)

    expect(wrapper.get('[data-testid="plugin-processing-message"]').text()).toContain('Plugin is in use.')
    expect(wrapper.get('[data-testid="ui-step-download"]').attributes('data-state')).toBe(UiStepState.Failed)
    expect(wrapper.find('[data-testid="plugin-processing-restart"]').exists()).toBe(false)
  })
})
