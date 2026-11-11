/**
 * Mount tests for ClientGroupSelect: it loads the groups from the store, offers the caller's empty
 * row first and reports the picked id (null for the empty row).
 */
import { flushPromises, mount } from '@vue/test-utils'
import { createPinia } from 'pinia'
import { afterEach, describe, expect, it, vi } from 'vitest'
import ClientGroupSelect from '@/components/clients/ClientGroupSelect.vue'
import UiSelect from '@/components/ui/UiSelect.vue'
import { createTestI18n } from '../../helpers/i18n'
import { json, stubFetch } from '../../helpers/fetch'

/** Two groups as the list endpoint answers them. */
const GROUPS = [
  { id: 1, name: 'VIP', description: null, discountPercent: 10, color: '#112233', isDefault: false, clientCount: 2 },
  { id: 2, name: 'Partners', description: null, discountPercent: 5, color: '#445566', isDefault: true, clientCount: 0 },
]

/**
 * Mounts the picker and waits for the groups to load.
 *
 * @param modelValue - The chosen group id.
 * @returns The wrapper.
 */
const mountSelect = async (modelValue: number | null = null) => {
  stubFetch(json(GROUPS))
  const wrapper = mount(ClientGroupSelect, {
    props: { modelValue, emptyLabel: 'Any' },
    global: { plugins: [createTestI18n(), createPinia()] },
  })
  await flushPromises()
  return wrapper
}

afterEach(() => {
  vi.unstubAllGlobals()
})

describe('ClientGroupSelect', () => {
  it('offers the empty row, then one row per group', async () => {
    const wrapper = await mountSelect()

    expect((wrapper.getComponent(UiSelect).props() as { options: unknown[] }).options).toEqual([
      { value: null, label: 'Any' },
      { value: 1, label: 'VIP' },
      { value: 2, label: 'Partners' },
    ])
  })

  it('relays the picked id', async () => {
    const wrapper = await mountSelect()

    wrapper.getComponent(UiSelect).vm.$emit('update:modelValue', 2)

    expect(wrapper.emitted('update:modelValue')).toEqual([[2]])
  })
})
