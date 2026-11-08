/**
 * Tests for useFieldErrorClear: a field's message is reported for clearing only when that field is
 * edited while it holds a message, and other fields are left alone.
 */
import { describe, expect, it } from 'vitest'
import { defineComponent, nextTick, ref } from 'vue'
import { mount } from '@vue/test-utils'
import { useFieldErrorClear } from '@/composables/useFieldErrorClear'

/**
 * Mounts a throwaway component that owns two fields and the helper.
 *
 * @param messages - The API messages the helper reads.
 * @returns The two field refs and the list of cleared field names.
 */
const mountHelper = (messages: Record<string, string>) => {
  /** The watched SKU field. */
  const sku = ref('')
  /** The watched metadata field. */
  const metadata = ref('')
  const cleared: string[] = []
  mount(defineComponent({
    setup: () => {
      useFieldErrorClear(() => messages, (field) => cleared.push(field), [
        ['sku', sku],
        ['metadata', metadata],
      ])
      return () => null
    },
  }))
  return { sku, metadata, cleared }
}

describe('useFieldErrorClear', () => {
  it('reports a field that is edited while it has a message', async () => {
    const { sku, cleared } = mountHelper({ sku: 'Taken.' })

    sku.value = 'A-1'
    await nextTick()

    expect(cleared).toEqual(['sku'])
  })

  it('reports nothing for an edited field without a message', async () => {
    const { metadata, cleared } = mountHelper({ sku: 'Taken.' })

    metadata.value = '{}'
    await nextTick()

    expect(cleared).toEqual([])
  })

  it('reports only the field that changed', async () => {
    const { sku, metadata, cleared } = mountHelper({ sku: 'Taken.', metadata: 'Too large.' })

    metadata.value = '{}'
    await nextTick()
    sku.value = 'B'
    await nextTick()

    expect(cleared).toEqual(['metadata', 'sku'])
  })
})
