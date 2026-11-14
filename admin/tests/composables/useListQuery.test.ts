/**
 * Tests for the shared parts of a list screen: loading when the address names another list (and not when the reader
 * leaves for another screen), knowing whether the list is narrowed, reading a filter as an enum member or an id, and
 * building the chips of the applied filters.
 */
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { defineComponent, h } from 'vue'
import { flushPromises, mount } from '@vue/test-utils'
import { createMemoryHistory, createRouter } from 'vue-router'
import { useListQuery } from '@/composables/useListQuery'
import type { ListQuery } from '@/composables/useListQuery'

/** The filter fields of the probe screen. */
const KEYS = ['status', 'groupId'] as const

/** The closed set the probe's status filter takes. */
const Status = { Active: 'Active', Closed: 'Closed' } as const

/**
 * Mounts a screen that uses the composable, at the given address.
 *
 * @param url - The address the router starts at.
 * @returns The router, the load spy and a reader of the composable's result.
 */
const setup = async (url: string) => {
  let state: ListQuery<(typeof KEYS)[number]> | null = null
  const load = vi.fn()
  const Screen = defineComponent({
    setup() {
      state = useListQuery(KEYS)
      state.loadOnChange(load)
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
  mount({ render: () => h('div', [h(Screen)]) }, { global: { plugins: [router] } })
  await flushPromises()
  return { router, load, list: () => state as unknown as ListQuery<(typeof KEYS)[number]> }
}

beforeEach(() => {
  // Only the search pause is faked: the router and `flushPromises` must keep running on real scheduling.
  vi.useFakeTimers({ toFake: ['setTimeout', 'clearTimeout'] })
})

afterEach(() => {
  vi.useRealTimers()
})

describe('useListQuery', () => {
  it('loads once when the screen mounts', async () => {
    const { load } = await setup('/list')

    expect(load).toHaveBeenCalledTimes(1)
  })

  it('loads again when a filter is applied or the page changes', async () => {
    const { load, list } = await setup('/list')

    list().draft.value.status = Status.Active
    await list().apply()
    await flushPromises()
    expect(load).toHaveBeenCalledTimes(2)

    await list().setPage(3)
    await flushPromises()
    expect(load).toHaveBeenCalledTimes(3)
  })

  it('does not load again when the reader leaves for another screen', async () => {
    const { router, load } = await setup('/list?status=Active')

    await router.push('/other?anything=1')
    await flushPromises()

    expect(load).toHaveBeenCalledTimes(1)
  })

  it('knows the list is narrowed by a filter or by the search, and not otherwise', async () => {
    expect((await setup('/list')).list().narrowed.value).toBe(false)
    expect((await setup('/list?status=Active')).list().narrowed.value).toBe(true)
    expect((await setup('/list?search=acme')).list().narrowed.value).toBe(true)
  })

  it('steps back one page when a delete left the page empty, and only then', async () => {
    const { router, list } = await setup('/list?page=3')

    await list().stepBackWhenEmpty(false)
    expect(router.currentRoute.value.query.page).toBe('3')

    await list().stepBackWhenEmpty(true)
    expect(router.currentRoute.value.query.page).toBe('2')

    await list().stepBackWhenEmpty(true)
    expect(router.currentRoute.value.query.page).toBeUndefined()

    await list().stepBackWhenEmpty(true)
    expect(router.currentRoute.value.query.page).toBeUndefined()
  })

  it('reads a filter as a member of a closed set and ignores a value that is not one', async () => {
    const known = (await setup('/list?status=Closed')).list().enumOf('status', Status)
    const unknown = (await setup('/list?status=Nonsense')).list().enumOf('status', Status)
    const unset = (await setup('/list')).list().enumOf('status', Status)

    expect(known.value).toBe(Status.Closed)
    expect(unknown.value).toBeUndefined()
    expect(unset.value).toBeUndefined()
  })

  it('reads a filter as an id and ignores anything that is not a positive whole number', async () => {
    expect((await setup('/list?groupId=12')).list().idOf('groupId').value).toBe(12)
    expect((await setup('/list?groupId=abc')).list().idOf('groupId').value).toBeUndefined()
    expect((await setup('/list?groupId=0')).list().idOf('groupId').value).toBeUndefined()
    expect((await setup('/list?groupId=1.5')).list().idOf('groupId').value).toBeUndefined()
    expect((await setup('/list')).list().idOf('groupId').value).toBeUndefined()
  })

  it('reads a filter as a day and ignores anything that is not exactly YYYY-MM-DD', async () => {
    expect((await setup('/list?status=2026-03-10')).list().dateOf('status').value).toBe('2026-03-10')
    expect((await setup('/list?status=soon')).list().dateOf('status').value).toBeUndefined()
    expect((await setup('/list?status=2026-03-10T00:00:00Z')).list().dateOf('status').value).toBeUndefined()
    expect((await setup('/list?status=2026-3-1')).list().dateOf('status').value).toBeUndefined()
    expect((await setup('/list?status=2026-13-45')).list().dateOf('status').value).toBeUndefined()
    expect((await setup('/list')).list().dateOf('status').value).toBeUndefined()
  })

  it('builds a chip for each filter that has a value, in the order described', async () => {
    const { list } = await setup('/list?status=Closed')
    const chips = list().chipsOf(() => [
      { key: 'groupId', label: 'Group', value: undefined },
      { key: 'status', label: 'Status', value: 'Closed' },
      { key: 'groupId', label: 'Group', value: '' },
    ])

    expect(chips.value).toEqual([{ key: 'status', label: 'Status', value: 'Closed' }])
  })
})
