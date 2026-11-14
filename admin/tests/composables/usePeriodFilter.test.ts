/**
 * Tests for the period filter the billing lists share: the first and the last day are read from the address as bare
 * days, anything that is not a day is ignored, and both ends are described for their chips with the shared labels.
 */
import { describe, expect, it } from 'vitest'
import { defineComponent, h } from 'vue'
import { flushPromises, mount } from '@vue/test-utils'
import { createMemoryHistory, createRouter } from 'vue-router'
import { useListQuery } from '@/composables/useListQuery'
import { usePeriodFilter } from '@/composables/usePeriodFilter'
import type { PeriodFilter } from '@/composables/usePeriodFilter'
import { createTestI18n } from '../helpers/i18n'

/** The address keys of the probe screen's period. */
const KEYS = { from: 'from', to: 'to' } as const

/**
 * Mounts a screen that uses the composable, at the given address.
 *
 * @param url - The address the router starts at.
 * @returns A reader of the period the screen holds.
 */
const setup = async (url: string) => {
  let state: PeriodFilter<'from' | 'to'> | null = null
  const Screen = defineComponent({
    setup() {
      state = usePeriodFilter(useListQuery(['from', 'to'] as const), KEYS)
      return () => h('div')
    },
  })
  const router = createRouter({ history: createMemoryHistory(), routes: [{ path: '/list', component: Screen }] })
  await router.push(url)
  await router.isReady()
  mount({ render: () => h('div', [h(Screen)]) }, { global: { plugins: [router, createTestI18n()] } })
  await flushPromises()
  return () => state as unknown as PeriodFilter<'from' | 'to'>
}

describe('usePeriodFilter', () => {
  it('reads both days from the address as written', async () => {
    const period = await setup('/list?from=2026-10-01&to=2026-10-31')

    expect(period().from.value).toBe('2026-10-01')
    expect(period().to.value).toBe('2026-10-31')
  })

  it('ignores a value that is not a bare day, such as a timestamp', async () => {
    const period = await setup('/list?from=2026-10-01T00:00:00Z&to=yesterday')

    expect(period().from.value).toBeUndefined()
    expect(period().to.value).toBeUndefined()
  })

  it('describes each applied end for its chip with the shared labels', async () => {
    const period = await setup('/list?from=2026-10-01&to=2026-10-31')

    expect(period().chipSources()).toEqual([
      { key: 'from', label: 'Date from', value: '01/10/2026' },
      { key: 'to', label: 'Date to', value: '31/10/2026' },
    ])
  })

  it('describes an end that is not set without a value', async () => {
    const period = await setup('/list')

    expect(period().chipSources().every((source) => source.value === undefined)).toBe(true)
  })
})
