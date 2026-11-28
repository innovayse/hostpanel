/**
 * Tests for the plugin processing state: it opens while the work runs and cannot be closed meanwhile, ends with the staged
 * note or the API sentence, stays open through the restart the host runs by itself, and closes by itself only when the
 * outcome is handed to another prompt.
 */
import { describe, expect, it, vi } from 'vitest'
import { defineComponent, h } from 'vue'
import { mount } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import { usePluginProcessing } from '@/composables/usePluginProcessing'
import { pluginProgressView } from '@/lib/pluginProgress'
import { usePluginsStore } from '@/stores/plugins'
import { PluginProcessingPhase, PluginProcessingPlan, PluginProcessingStep, PluginRestartState } from '@/types/plugin'
import type { PluginActionOutcome } from '@/types/plugin'
import { json } from '../helpers/fetch'
import { createTestI18n } from '../helpers/i18n'

/**
 * Mounts a throwaway component that owns the state, so the i18n and Pinia contexts are real.
 *
 * @returns The processing state.
 */
const mountProcessing = () => {
  const pinia = createPinia()
  setActivePinia(pinia)
  let processing: ReturnType<typeof usePluginProcessing> | undefined
  mount(defineComponent({
    setup: () => {
      processing = usePluginProcessing()
      return () => h('div')
    },
  }), { global: { plugins: [createTestI18n(), pinia] } })
  return processing as ReturnType<typeof usePluginProcessing>
}

/**
 * Builds a finished outcome.
 *
 * @param fields - The members to change.
 * @returns The outcome.
 */
const outcomeOf = (fields: Partial<PluginActionOutcome>): PluginActionOutcome =>
  ({ ok: true, stagedVersion: null, reloaded: false, restarting: false, error: null, ...fields })

describe('usePluginProcessing', () => {
  it('is open and busy while the work is pending, and cannot be closed meanwhile', async () => {
    const processing = mountProcessing()
    let finish: (value: PluginActionOutcome) => void = () => undefined
    const pending = processing.run({
      name: 'Example Pay',
      step: PluginProcessingStep.Updating,
      plan: PluginProcessingPlan.Registry,
      version: '2.0.0',
      work: () => new Promise(resolve => {
        finish = resolve
      }),
    })

    expect(processing.open.value).toBe(true)
    expect(processing.busy.value).toBe(true)
    expect(processing.version.value).toBe('2.0.0')
    expect(processing.view.value.phase).toBe(PluginProcessingPhase.Running)
    processing.close()
    expect(processing.open.value).toBe(true)

    finish(outcomeOf({ reloaded: true }))
    await pending
    expect(processing.busy.value).toBe(false)
    expect(processing.view.value).toEqual(pluginProgressView({
      plan: PluginProcessingPlan.Registry,
      progress: null,
      outcome: outcomeOf({ reloaded: true }),
      restartState: PluginRestartState.Idle,
      restartFailed: false,
    }))
    expect(processing.view.value.percent).toBe(100)
    expect(processing.message.value).toContain('2.0.0')
    processing.close()
    expect(processing.open.value).toBe(false)
  })

  it('keeps the API sentence of a refusal', async () => {
    const processing = mountProcessing()

    const outcome = await processing.run({
      name: 'Example Pay',
      step: PluginProcessingStep.Removing,
      plan: PluginProcessingPlan.Removal,
      work: async () => outcomeOf({ ok: false, error: 'Plugin is in use.' }),
    })

    expect(outcome.ok).toBe(false)
    expect(processing.failed.value).toBe(true)
    expect(processing.message.value).toBe('Plugin is in use.')
  })

  it('notes the staged version of a success the host does not restart for', async () => {
    const processing = mountProcessing()

    await processing.run({
      name: 'Example Pay',
      step: PluginProcessingStep.Installing,
      plan: PluginProcessingPlan.Archive,
      work: async () => outcomeOf({ stagedVersion: '2.0.0' }),
    })

    expect(processing.message.value).toContain('2.0.0')
    expect(processing.open.value).toBe(true)
  })

  it('stays open on the restart step when the host restarts itself to apply the change', async () => {
    vi.useFakeTimers()
    vi.stubGlobal('fetch', vi.fn(async () => json({ status: 'Healthy', timestamp: 't', processStartedAt: 'A' })))
    const processing = mountProcessing()

    await processing.run({
      name: 'Example Pay',
      step: PluginProcessingStep.Updating,
      plan: PluginProcessingPlan.Registry,
      work: async () => outcomeOf({ restarting: true }),
    })

    expect(processing.open.value).toBe(true)
    expect(processing.busy.value).toBe(true)
    expect(usePluginsStore().restartState).toBe(PluginRestartState.Restarting)
    usePluginsStore().resetRestart()
    vi.useRealTimers()
    vi.unstubAllGlobals()
  })

  it('closes by itself when the outcome is handed to another prompt', async () => {
    const processing = mountProcessing()

    await processing.run({
      name: 'new.zip',
      step: PluginProcessingStep.Uploading,
      plan: PluginProcessingPlan.ArchiveRead,
      work: async () => outcomeOf({}),
      handsOff: outcome => outcome.ok,
    })

    expect(processing.open.value).toBe(false)
  })
})
