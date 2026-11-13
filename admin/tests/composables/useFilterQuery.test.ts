/**
 * Tests for the URL-synced filter state: the address is read and written, the search waits for a pause, and any
 * change of a filter or of the search takes the list back to its first page.
 */
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { defineComponent, h, watch } from 'vue'
import { flushPromises, mount } from '@vue/test-utils'
import { createMemoryHistory, createRouter } from 'vue-router'
import { useFilterQuery } from '@/composables/useFilterQuery'
import type { FilterQuery } from '@/composables/useFilterQuery'
import { SEARCH_DEBOUNCE_MS } from '@/lib/limits'

/** The filter fields of the probe screen. */
const KEYS = ['status', 'group'] as const

/**
 * Mounts a screen that uses the composable, at the given address.
 *
 * @param url - The address the router starts at.
 * @returns The router, the wrapper and a reader of the composable's result.
 */
const setup = async (url: string) => {
  let state: FilterQuery<(typeof KEYS)[number]> | null = null
  const Screen = defineComponent({
    setup() {
      state = useFilterQuery(KEYS)
      return () => h('div')
    },
  })
  const router = createRouter({
    history: createMemoryHistory(),
    routes: [
      { path: '/list', component: Screen },
      { path: '/other', component: { render: () => h('div') } },
    ],
  })
  await router.push(url)
  await router.isReady()
  const wrapper = mount({ render: () => h('div', [h(Screen)]) }, { global: { plugins: [router] } })
  return { router, wrapper, filters: () => state as unknown as FilterQuery<(typeof KEYS)[number]> }
}

beforeEach(() => {
  // Only the search pause is faked: the router and `flushPromises` must keep running on real scheduling.
  vi.useFakeTimers({ toFake: ['setTimeout', 'clearTimeout'] })
})

afterEach(() => {
  vi.useRealTimers()
})

describe('useFilterQuery', () => {
  it('reads the filters, the search text and the page from the address', async () => {
    const { filters } = await setup('/list?status=active&search=acme&page=3')

    expect(filters().applied.value).toEqual({ status: 'active', group: '' })
    expect(filters().search.value).toBe('acme')
    expect(filters().page.value).toBe(3)
    expect(filters().activeCount.value).toBe(1)
  })

  it('treats a bad page as the first one', async () => {
    const { filters } = await setup('/list?page=abc')

    expect(filters().page.value).toBe(1)
  })

  it('writes the draft on apply, drops the page, and keeps the page on an unchanged apply', async () => {
    const { router, filters } = await setup('/list?page=4')
    filters().draft.value.status = 'active'

    await filters().apply()

    expect(router.currentRoute.value.query).toEqual({ status: 'active' })
    await router.push({ query: { status: 'active', page: '2' } })
    await filters().apply()
    expect(router.currentRoute.value.query.page).toBe('2')
  })

  it('empties the draft on reset without touching the address, and restores it on cancel', async () => {
    const { router, filters } = await setup('/list?status=active')

    filters().reset()
    expect(filters().draft.value).toEqual({ status: '', group: '' })
    expect(router.currentRoute.value.query.status).toBe('active')

    filters().cancel()
    expect(filters().draft.value.status).toBe('active')
  })

  it('removes one filter and clears all of them at once, keeping the search', async () => {
    const { router, filters } = await setup('/list?status=a&group=b&search=x&page=2')

    await filters().remove('status')
    expect(router.currentRoute.value.query).toEqual({ group: 'b', search: 'x' })

    await filters().clearAll()
    expect(router.currentRoute.value.query).toEqual({ search: 'x' })
  })

  it('brings the draft along when the address changes', async () => {
    const { router, filters } = await setup('/list')

    await router.push({ query: { group: 'vip' } })
    await flushPromises()

    expect(filters().draft.value.group).toBe('vip')
  })

  it('writes the search once after a pause, trimmed, and returns to the first page', async () => {
    const { router, filters } = await setup('/list?page=5')

    filters().search.value = 'a'
    await flushPromises()
    filters().search.value = ' acme '
    await flushPromises()
    vi.advanceTimersByTime(SEARCH_DEBOUNCE_MS - 1)
    expect(router.currentRoute.value.query.search).toBeUndefined()
    vi.advanceTimersByTime(1)
    await flushPromises()

    expect(router.currentRoute.value.query).toEqual({ search: 'acme' })
  })

  it('exposes the search the address holds, not the text still being typed', async () => {
    const { filters } = await setup('/list?search=acme')
    expect(filters().appliedSearch.value).toBe('acme')

    filters().search.value = 'acme corp'
    await flushPromises()
    expect(filters().appliedSearch.value).toBe('acme')

    vi.advanceTimersByTime(SEARCH_DEBOUNCE_MS)
    await flushPromises()
    expect(filters().appliedSearch.value).toBe('acme corp')
  })

  it('shows the search the address holds after the back button', async () => {
    const { router, filters } = await setup('/list?search=old')

    await router.push({ query: { search: 'new' } })
    await flushPromises()

    expect(filters().search.value).toBe('new')
  })

  it('moves between pages, leaving no page key on the first', async () => {
    const { router, filters } = await setup('/list?status=a')

    await filters().setPage(3)
    expect(router.currentRoute.value.query).toEqual({ status: 'a', page: '3' })

    await filters().setPage(1)
    expect(router.currentRoute.value.query).toEqual({ status: 'a' })
  })

  it('does not write a pending search after the screen is gone', async () => {
    const { router, wrapper, filters } = await setup('/list')

    filters().search.value = 'late'
    await flushPromises()
    wrapper.unmount()
    vi.advanceTimersByTime(SEARCH_DEBOUNCE_MS * 2)
    await flushPromises()

    expect(router.currentRoute.value.query.search).toBeUndefined()
  })
  it('keeps the filters, the search and the page while another route is current', async () => {
    const { router, filters } = await setup('/list?status=a&search=x&page=3')
    const seen: number[] = []
    watch(filters().page, (value) => {
      seen.push(value)
    })

    await router.push('/other')
    await flushPromises()

    expect(filters().page.value).toBe(3)
    expect(filters().applied.value.status).toBe('a')
    expect(filters().appliedSearch.value).toBe('x')
    expect(seen).toEqual([])
  })
})
