/**
 * Tests for the plugin processing state: it opens while the work runs, ends with the staged note or the API
 * sentence, closes by itself only when the outcome is handed to another prompt, and ignores a close while running.
 */
import { describe, expect, it } from 'vitest'
import { defineComponent, h } from 'vue'
import { mount } from '@vue/test-utils'
import { usePluginProcessing } from '@/composables/usePluginProcessing'
import { PluginProcessingStep } from '@/types/plugin'
import { createTestI18n } from '../helpers/i18n'

/**
 * Mounts a throwaway component that owns the state, so the i18n context is real.
 *
 * @returns The processing state.
 */
const mountProcessing = () => {
  let processing: ReturnType<typeof usePluginProcessing> | undefined
  mount(defineComponent({
    setup: () => {
      processing = usePluginProcessing()
      return () => h('div')
    },
  }), { global: { plugins: [createTestI18n()] } })
  return processing as ReturnType<typeof usePluginProcessing>
}

describe('usePluginProcessing', () => {
  it('is open and running while the work is pending, and cannot be closed meanwhile', async () => {
    const processing = mountProcessing()
    let finish: (value: { ok: boolean; stagedVersion: string | null; error: string | null }) => void = () => undefined
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

    finish({ ok: true, stagedVersion: null, error: null })
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
      work: async () => ({ ok: false, stagedVersion: null, error: 'Plugin is in use.' }),
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
      work: async () => ({ ok: true, stagedVersion: '2.0.0', error: null }),
    })

    expect(processing.message.value).toContain('2.0.0')
    expect(processing.open.value).toBe(true)
  })

  it('closes by itself when the outcome is handed to another prompt', async () => {
    const processing = mountProcessing()

    await processing.run({
      name: 'new.zip',
      step: PluginProcessingStep.Uploading,
      work: async () => ({ ok: true, stagedVersion: null, error: null }),
      handsOff: outcome => outcome.ok,
    })

    expect(processing.open.value).toBe(false)
  })

  it('stays open when the outcome is not handed on, a refusal included', async () => {
    const processing = mountProcessing()

    await processing.run({
      name: 'new.zip',
      step: PluginProcessingStep.Installing,
      work: async () => ({ ok: false, stagedVersion: null, error: 'Refused.' }),
      handsOff: outcome => outcome.ok,
    })

    expect(processing.open.value).toBe(true)
    expect(processing.message.value).toBe('Refused.')
  })
})
