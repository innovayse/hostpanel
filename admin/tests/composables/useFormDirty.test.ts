/**
 * Tests for the form dirty tracker: an edit raises the flag, populating the form and then settling
 * leaves it clear (also when a watcher writes more state during the same flush), and a later edit
 * raises it again.
 */
import { describe, expect, it } from 'vitest'
import { defineComponent, h, reactive, ref, watch } from 'vue'
import { mount } from '@vue/test-utils'
import { useFormDirty } from '@/composables/useFormDirty'
import type { FormDirty } from '@/composables/useFormDirty'

/** The state a mounted probe form exposes. */
interface Probe {
  /** The tracker under test. */
  dirty: FormDirty
  /** A plain ref field. */
  name: ReturnType<typeof ref<string>>
  /** A nested reactive field. */
  texts: { en: { title: string } }
  /** A field a watcher fills from another one, as a product link fills its URL. */
  derived: ReturnType<typeof ref<string>>
}

/**
 * Mounts a form that tracks a ref and a reactive object, with a watcher deriving one field.
 *
 * @returns The probe state.
 */
const mountProbe = (): Probe => {
  /** The probe state set by the form's setup. */
  let probe: Probe | null = null
  const Form = defineComponent({
    setup() {
      /** A plain field. */
      const name = ref('')
      /** A field filled by a watcher. */
      const derived = ref('')
      /** A nested reactive field. */
      const texts = reactive({ en: { title: '' } })
      /** The tracker under test. */
      const dirty = useFormDirty([name, derived, texts])
      watch(name, (value) => {
        derived.value = `/products/${value}`
      })
      probe = { dirty, name, texts, derived }
      return () => h('div')
    },
  })
  mount(Form)
  return probe as unknown as Probe
}

describe('useFormDirty', () => {
  it('starts clean and turns dirty on an edit', async () => {
    const probe = mountProbe()
    expect(probe.dirty.isDirty.value).toBe(false)

    probe.texts.en.title = 'Hello'
    await Promise.resolve()
    await Promise.resolve()

    expect(probe.dirty.isDirty.value).toBe(true)
  })

  it('stays clean when the form is populated and then settled', async () => {
    const probe = mountProbe()

    probe.name.value = 'plan'
    probe.texts.en = { title: 'Loaded' }
    await probe.dirty.settle()

    expect(probe.derived.value).toBe('/products/plan')
    expect(probe.dirty.isDirty.value).toBe(false)
  })

  it('turns dirty again on an edit after settling', async () => {
    const probe = mountProbe()
    await probe.dirty.settle()

    probe.name.value = 'x'
    await Promise.resolve()
    await Promise.resolve()

    expect(probe.dirty.isDirty.value).toBe(true)
  })
})
