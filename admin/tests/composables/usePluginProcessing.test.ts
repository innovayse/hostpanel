/**
 * Tests for the plugin processing state: it opens while the work runs, ends with the staged note or the API
 * sentence, closes by itself only when the outcome is handed to another prompt, and ignores a close while running.
 */
import { describe, expect, it, vi } from 'vitest'
import { defineComponent, h } from 'vue'
import { mount } from '@vue/test-utils'
import { usePluginProcessing } from '@/composables/usePluginProcessing'
import { createPinia, setActivePinia } from 'pinia'
import type { Pinia } from 'pinia'
import { usePluginsStore } from '@/stores/plugins'
import { PluginProcessingStep, PluginRestartState } from '@/types/plugin'
import type { PluginActionOutcome } from '@/types/plugin'
import { json } from '../helpers/fetch'
import { createTestI18n } from '../helpers/i18n'

/**
 * Mounts a throwaway component that owns the state, so the i18n context is real.
 *
 * @param pinia - The Pinia the screen runs with, for a run that hands over to the restart flow.
 * @returns The processing state.
 */
const mountProcessing = (pinia?: Pinia) => {
  let processing: ReturnType<typeof usePluginProcessing> | undefined
  mount(defineComponent({
    setup: () => {
      processing = usePluginProcessing()
      return () => h('div')
    },
  }), { global: { plugins: pinia === undefined ? [createTestI18n()] : [createTestI18n(), pinia] } })
  return processing as ReturnType<typeof usePluginProcessing>
}

describe('usePluginProcessing', () => {
  it('is open and running while the work is pending, and cannot be closed meanwhile', async () => {
    const processing = mountProcessing()
    let finish: (value: PluginActionOutcome) => void = () => undefined
    const pending = processing.run({
      name: 'Example Pay',
      step: PluginProcessingStep.Updating,
      work: () => new Promise(resolve => {
        finish = resolve
      }),
    })

    expect(processing.open.value).toBe(true)
    expect(processing.running.value).toBe(true)
    expect(processing.name.value).toBe('Example Pay')
    expect(processing.step.value).toBe(PluginProcessingStep.Updating)
    processing.close()
    expect(processing.open.value).toBe(true)

    finish({ ok: true, stagedVersion: null, reloaded: false, restarting: false, error: null })
    await pending
    expect(processing.running.value).toBe(false)
    expect(processing.failed.value).toBe(false)
    expect(processing.message.value).toBeNull()
    processing.close()
    expect(processing.open.value).toBe(false)
  })

  it('keeps the API sentence of a refusal', async () => {
    const processing = mountProcessing()

    const outcome = await processing.run({
      name: 'Example Pay',
      step: PluginProcessingStep.Removing,
      work: async () => ({ ok: false, stagedVersion: null, reloaded: false, restarting: false, error: 'Plugin is in use.' }),
    })

    expect(outcome.ok).toBe(false)
    expect(processing.failed.value).toBe(true)
    expect(processing.message.value).toBe('Plugin is in use.')
  })

  it('notes the staged version of a success', async () => {
    const processing = mountProcessing()

    await processing.run({
      name: 'Example Pay',
      step: PluginProcessingStep.Installing,
      work: async () => ({ ok: true, stagedVersion: '2.0.0', reloaded: false, restarting: false, error: null }),
    })

    expect(processing.message.value).toContain('2.0.0')
    expect(processing.open.value).toBe(true)
  })

  it('notes that the plugin runs without a restart when the host reloaded it', async () => {
    const processing = mountProcessing()

    await processing.run({
      name: 'Example Pay',
      step: PluginProcessingStep.Updating,
      work: async () => ({ ok: true, stagedVersion: null, reloaded: true, restarting: false, error: null }),
    })

    expect(processing.failed.value).toBe(false)
    expect(processing.message.value).toBe('Example Pay was updated and runs without a restart.')
  })

  it('closes into the restart dialog when the host restarts itself to apply the change', async () => {
    vi.useFakeTimers()
    vi.stubGlobal('fetch', vi.fn(async () => json({ status: 'Healthy', timestamp: 't', processStartedAt: 'A' })))
    const pinia = createPinia()
    setActivePinia(pinia)
    const processing = mountProcessing(pinia)

    await processing.run({
      name: 'Example Pay',
      step: PluginProcessingStep.Updating,
      work: async () => ({ ok: true, stagedVersion: null, reloaded: false, restarting: true, error: null }),
    })

    expect(processing.open.value).toBe(false)
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
      work: async () => ({ ok: true, stagedVersion: null, reloaded: false, restarting: false, error: null }),
      handsOff: outcome => outcome.ok,
    })

    expect(processing.open.value).toBe(false)
  })

  it('stays open when the outcome is not handed on, a refusal included', async () => {
    const processing = mountProcessing()

    await processing.run({
      name: 'new.zip',
      step: PluginProcessingStep.Installing,
      work: async () => ({ ok: false, stagedVersion: null, reloaded: false, restarting: false, error: 'Refused.' }),
      handsOff: outcome => outcome.ok,
    })

    expect(processing.open.value).toBe(true)
    expect(processing.message.value).toBe('Refused.')
  })
})
