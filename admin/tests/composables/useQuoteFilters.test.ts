/**
 * Tests for the quote filters every quote list shares: the address is read back as the typed stage and validity the
 * API takes, a value that is not one is ignored, and each applied filter is described for its chip.
 */
import { describe, expect, it } from 'vitest'
import { defineComponent, h } from 'vue'
import { flushPromises, mount } from '@vue/test-utils'
import { createMemoryHistory, createRouter } from 'vue-router'
import { useListQuery } from '@/composables/useListQuery'
import { QuoteFilterKey, useQuoteFilters } from '@/composables/useQuoteFilters'
import type { QuoteFilterSet } from '@/composables/useQuoteFilters'
import { QuoteStage, QuoteValidity } from '@/types/quote'
import { createTestI18n } from '../helpers/i18n'

/**
 * Mounts a screen that uses the composables, at the given address.
 *
 * @param url - The address the router starts at.
 * @returns A reader of the quote filters the screen holds.
 */
const setup = async (url: string) => {
  let state: QuoteFilterSet | null = null
  const Screen = defineComponent({
    setup() {
      const list = useListQuery(Object.values(QuoteFilterKey))
      state = useQuoteFilters(list)
      return () => h('div')
    },
  })
  const router = createRouter({ history: createMemoryHistory(), routes: [{ path: '/quotes', component: Screen }] })
  await router.push(url)
  await router.isReady()
  mount({ render: () => h('div', [h(Screen)]) }, { global: { plugins: [router, createTestI18n()] } })
  await flushPromises()
  return () => state as unknown as QuoteFilterSet
}

describe('useQuoteFilters', () => {
  it('reads the stage and the validity from the address as the API takes them', async () => {
    const filters = await setup('/quotes?stage=OnHold&validity=Expired')

    expect(filters().filters.value).toEqual({ stage: QuoteStage.OnHold, validity: QuoteValidity.Expired })
  })

  it('leaves out a filter that is not set or holds a value that is not a member', async () => {
    const filters = await setup('/quotes?stage=Archived&validity=Maybe')

    expect(filters().filters.value).toEqual({ stage: undefined, validity: undefined })
  })

  it('describes each applied filter for its chip', async () => {
    const filters = await setup('/quotes?stage=OnHold&validity=Valid')

    expect(filters().chipSources()).toEqual([
      { key: 'stage', label: 'Stage', value: 'On Hold' },
      { key: 'validity', label: 'Validity Period', value: 'Valid' },
    ])
  })

  it('describes a filter that is not set without a value', async () => {
    const filters = await setup('/quotes')

    expect(filters().chipSources().every((source) => source.value === undefined)).toBe(true)
  })
})
