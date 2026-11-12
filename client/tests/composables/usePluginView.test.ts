/**
 * Tests for `composables/usePluginView.ts`, through the real store, API composable and transport
 * against a stub server: a block outside a view is a host bug; a source is read on mount, on a
 * refresh of its block id and on a change of its query; a refusal shows the API's sentence; and a
 * source the page cannot read calls nothing and says nothing.
 *
 * @module tests/composables/usePluginView.test
 */

import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { defineComponent, h, nextTick, ref } from 'vue'
import { enableAutoUnmount } from '@vue/test-utils'
import { startHeldServer } from '../helpers/heldServer'
import { startTransport } from '../helpers/stubTransport'
import type { StubServer } from '../helpers/stubServer'
import { mountWithKit } from '~/tests/helpers/mountWithKit'
import { usePluginsStore } from '~/stores/plugins'
import { providePluginView, usePluginCondition, usePluginFollowUp, usePluginSource, usePluginView } from '~/composables/usePluginView'
import { PLUGIN_FOLLOW_UP_INTERVAL_MS, PLUGIN_FOLLOW_UP_MAX_ATTEMPTS } from '~/constants/pluginFollowUp'
import { PLUGIN_VIEW_READ_MAX_AGE_MS } from '~/constants/pluginViewReads'
import type { PluginSource } from '~/types/pluginblock'
import type { PluginFollowUp } from '~/types/pluginfollowup'
import type { PluginViewCondition } from '~/types/pluginviewcondition'
import { settle } from '~/tests/helpers/settle'
import { waitUntil } from '~/tests/helpers/waitUntil'

/** The stub playing the BFF. */
let stub: StubServer

/** The refresh counts the test view hands out. */
const refreshes = ref<Record<string, number>>({})

/** The query the test block reads with. */
const query = ref<Record<string, string>>({})

/** The source the test block reads; tests replace it. */
const source = ref<PluginSource>({ route: { method: 'get', path: '/sites/{siteId}' }, field: 'status' })

/** What the recording view context was told: the keys watches were adopted under, and how many were let go of. */
const adopted = { keys: [] as string[], released: 0 }

/**
 * The view context's `adopt` for these tests: it only records, because keeping and ending watches is the view's own
 * work and is tested against the real `PluginView` (tests/components/plugin/View.test.ts).
 *
 * @param key - Who the watch belongs to.
 * @returns Lets go of the watch, which is counted.
 */
const recordAdoption = (key: string): (() => void) => {
  adopted.keys.push(key)
  return () => {
    adopted.released += 1
  }
}

/** A block that reads one source and shows the value, the error and whether it is unavailable. */
const Reader = defineComponent({
  setup() {
    const { value, error, unavailable } = usePluginSource(() => source.value, () => 'status', () => query.value)
    return () => h('div', [
      h('span', { 'data-testid': 'value' }, String(value.value ?? '')),
      h('span', { 'data-testid': 'error' }, error.value),
      h('span', { 'data-testid': 'unavailable' }, String(unavailable.value)),
    ])
  },
})

/** A view holding the reading block, for the page `/sites/7` of the test plugin. */
const Host = defineComponent({
  setup() {
    providePluginView({
      pluginId: 'acme-sites',
      values: { siteId: '7' },
      externalOrigins: [],
      frameOrigins: [],
      refreshes,
      refresh: () => undefined,
      open: () => undefined,
      adopt: recordAdoption,
      track: () => undefined,
    })
    return () => h(Reader)
  },
})

/**
 * The route calls the stub received.
 *
 * @returns The URLs, oldest first.
 */
const calls = (): string[] => stub.requests.filter(request => request.url.includes('/routes/')).map(request => request.url)

beforeEach(async () => {
  refreshes.value = {}
  query.value = {}
  source.value = { route: { method: 'get', path: '/sites/{siteId}' }, field: 'status' }
  let reads = 0
  stub = await startTransport(() => {
    reads += 1
    return { body: { status: ['a', 'b', 'c'][reads - 1] ?? 'z' } }
  })
})

// The test refs live at module level: a reader left mounted by an earlier test would keep reading from the next stub.
enableAutoUnmount(afterEach)

afterEach(async () => {
  await stub.close()
})

describe('usePluginView', () => {
  it('keeps the latest read when an older one answers after it', async () => {
    const server = await startHeldServer()
    try {
      const wrapper = mountWithKit(Host)
      await waitUntil(() => expect(server.held).toHaveLength(1))
      query.value = { page: '2' }
      await nextTick()
      await waitUntil(() => expect(server.held).toHaveLength(2))

      server.held[1]?.answer({ status: 'newer' })
      // Real sockets: the newer answer is awaited until it is shown, so the older one surely lands after it.
      await waitUntil(() => expect(wrapper.get('[data-testid="value"]').text()).toBe('newer'))
      server.held[0]?.answer({ status: 'older' })
      await settle()
      await new Promise(resolve => setTimeout(resolve, 20))

      expect(wrapper.get('[data-testid="value"]').text()).toBe('newer')
    } finally {
      await server.close()
    }
  })

  it('refuses a block rendered outside a view', () => {
    let refusal: unknown
    const Lost = defineComponent({
      setup() {
        try {
          usePluginView()
        } catch (error: unknown) {
          refusal = error
        }
        return () => h('span')
      },
    })

    mountWithKit(Lost)

    expect(String(refusal)).toContain('outside a plugin view')
  })

  it('reads on mount, on a refresh of its own id and when its query changes', async () => {
    const wrapper = mountWithKit(Host)
    await settle()
    expect(wrapper.get('[data-testid="value"]').text()).toBe('a')

    refreshes.value = { other: 1 }
    await nextTick()
    await settle()
    expect(calls()).toHaveLength(1)

    refreshes.value = { other: 1, status: 1 }
    await nextTick()
    await settle()
    expect(wrapper.get('[data-testid="value"]').text()).toBe('b')

    query.value = { page: '2' }
    await nextTick()
    await settle()
    expect(wrapper.get('[data-testid="value"]').text()).toBe('c')
    expect(calls()).toEqual([
      '/api/portal/client/plugins/acme-sites/routes/sites/7',
      '/api/portal/client/plugins/acme-sites/routes/sites/7',
      '/api/portal/client/plugins/acme-sites/routes/sites/7?page=2',
    ])
  })

  it('shows the API\'s sentence when the read is refused, and no value', async () => {
    stub.respondWith(() => ({ status: 403, body: { statusMessage: 'You may not see this site.', data: { code: 'CLIENT_PERMISSION_DENIED' } } }))

    const wrapper = mountWithKit(Host)
    await settle()

    expect(wrapper.get('[data-testid="value"]').text()).toBe('')
    expect(wrapper.get('[data-testid="error"]').text()).toBe('You may not see this site.')
    expect(wrapper.get('[data-testid="unavailable"]').text()).toBe('false')
  })

  it.each([
    [{ method: 'post', path: '/sites/{siteId}' }, 'a source that is not a GET'],
    [{ method: 'get', path: '/sites/{siteId}/posts/{postId}' }, 'a parameter the page does not carry'],
  ])('reads nothing for %j (%s) and has no sentence of its own', async (route, _reason: string) => {
    source.value = { route, field: 'status' }

    const wrapper = mountWithKit(Host)
    await settle()

    expect(calls()).toHaveLength(0)
    expect(wrapper.get('[data-testid="unavailable"]').text()).toBe('true')
    expect(wrapper.get('[data-testid="error"]').text()).toBe('')
  })
})

/** The condition the test block shows by; tests replace it. */
const condition = ref<PluginViewCondition | undefined>(undefined)

/** The ids the test view was asked to refresh, one entry per call. */
const refreshed: string[][] = []

/** The block ids whose refresh reads the test block's condition again. */
const watched = ref<string[]>(['status'])

/** Starts the test block's follow-up; set when the block mounts. */
let follow: (followUp: PluginFollowUp | undefined, settled?: () => void) => void = () => undefined

/** A block that shows `shown` while its condition holds and can start a follow-up. */
const Watcher = defineComponent({
  setup() {
    const visible = usePluginCondition(() => condition.value, () => watched.value)
    follow = usePluginFollowUp(() => 'publish')
    return () => h('span', { 'data-testid': 'visible' }, visible.value ? 'shown' : 'hidden')
  },
})

/** A view holding the watching block, recording every refresh it is asked for. */
const WatchHost = defineComponent({
  setup() {
    providePluginView({
      pluginId: 'acme-sites',
      values: { siteId: '7' },
      externalOrigins: [],
      frameOrigins: [],
      refreshes,
      refresh: (ids: string[]) => {
        refreshed.push(ids)
      },
      open: () => undefined,
      adopt: recordAdoption,
      track: () => undefined,
    })
    return () => h(Watcher)
  },
})

/** The condition every test below watches: the site is live. */
const LIVE: PluginViewCondition = { route: { method: 'get', path: '/sites/{siteId}' }, field: 'status', equals: ['live'] }

/** A follow-up that refreshes the status block until the site is live. */
const UNTIL_LIVE: PluginFollowUp = { refresh: ['status'], until: LIVE }

/**
 * Moves the faked clock and lets the reads it started answer.
 *
 * @param ms - How far to move it.
 * @returns Resolves once the replies have been handled.
 */
const advance = async (ms: number): Promise<void> => {
  vi.advanceTimersByTime(ms)
  await settle()
}

describe('usePluginCondition', () => {
  beforeEach(() => {
    condition.value = undefined
    refreshed.length = 0
    watched.value = ['status']
  })

  it('shows a block with no condition and reads nothing', async () => {
    const wrapper = mountWithKit(WatchHost)
    await settle()

    expect(wrapper.get('[data-testid="visible"]').text()).toBe('shown')
    expect(calls()).toHaveLength(0)
  })

  it('hides the block while reading and until the field matches, and shows it once a refresh of its id finds a match', async () => {
    stub.respondWith(() => ({ body: { status: 'building' } }))
    condition.value = LIVE

    const wrapper = mountWithKit(WatchHost)
    expect(wrapper.get('[data-testid="visible"]').text()).toBe('hidden')
    await settle()
    expect(wrapper.get('[data-testid="visible"]').text()).toBe('hidden')

    stub.respondWith(() => ({ body: { status: 'live' } }))
    refreshes.value = { status: 1 }
    await nextTick()
    await settle()

    expect(wrapper.get('[data-testid="visible"]').text()).toBe('shown')
    expect(calls()).toHaveLength(2)
  })

  it('reads again when any of several named blocks is refreshed, and for no other block', async () => {
    stub.respondWith(() => ({ body: { status: 'building' } }))
    condition.value = LIVE
    watched.value = ['top', 'versions']

    const wrapper = mountWithKit(WatchHost)
    await settle()
    expect(calls()).toHaveLength(1)

    refreshes.value = { elsewhere: 1 }
    await nextTick()
    await settle()
    expect(calls()).toHaveLength(1)

    stub.respondWith(() => ({ body: { status: 'live' } }))
    refreshes.value = { elsewhere: 1, versions: 1 }
    await nextTick()
    await settle()

    expect(calls()).toHaveLength(2)
    expect(wrapper.get('[data-testid="visible"]').text()).toBe('shown')
  })

  it('reads once, on mounting, when it names no block', async () => {
    stub.respondWith(() => ({ body: { status: 'live' } }))
    condition.value = LIVE
    watched.value = []

    mountWithKit(WatchHost)
    await settle()
    refreshes.value = { status: 1 }
    await nextTick()
    await settle()

    expect(calls()).toHaveLength(1)
  })

  it('hides the block when the read is refused or the field is absent', async () => {
    condition.value = LIVE
    stub.respondWith(() => ({ status: 403, body: { statusMessage: 'No.', data: { code: 'CLIENT_PERMISSION_DENIED' } } }))
    const refused = mountWithKit(WatchHost)
    await settle()
    expect(refused.get('[data-testid="visible"]').text()).toBe('hidden')

    stub.respondWith(() => ({ body: {} }))
    const absent = mountWithKit(WatchHost)
    await settle()
    expect(absent.get('[data-testid="visible"]').text()).toBe('hidden')
  })

  it('hides the block and reads nothing for a condition on a route that is not a GET', async () => {
    condition.value = { ...LIVE, route: { method: 'post', path: '/sites/{siteId}' } }

    const wrapper = mountWithKit(WatchHost)
    await settle()

    expect(wrapper.get('[data-testid="visible"]').text()).toBe('hidden')
    expect(calls()).toHaveLength(0)
  })
})

describe('usePluginFollowUp', () => {
  beforeEach(() => {
    condition.value = undefined
    refreshed.length = 0
    adopted.keys.length = 0
    adopted.released = 0
    // Only the follow-up's own wait is faked; `setImmediate` stays real so the stub's replies arrive.
    vi.useFakeTimers({ toFake: ['setTimeout', 'clearTimeout'] })
  })

  afterEach(() => {
    vi.useRealTimers()
  })

  it('waits the host interval, then reads and refreshes until the value is final, refreshing once more on it', async () => {
    let reads = 0
    stub.respondWith(() => ({ body: { status: ++reads < 3 ? 'building' : 'live' } }))
    mountWithKit(WatchHost)
    await settle()

    follow(UNTIL_LIVE)
    await advance(PLUGIN_FOLLOW_UP_INTERVAL_MS - 1)
    expect(calls()).toHaveLength(0)

    await advance(1)
    expect(calls()).toHaveLength(1)
    expect(refreshed).toEqual([['status']])

    await advance(PLUGIN_FOLLOW_UP_INTERVAL_MS)
    await advance(PLUGIN_FOLLOW_UP_INTERVAL_MS)
    expect(calls()).toHaveLength(3)
    expect(refreshed).toHaveLength(3)

    await advance(PLUGIN_FOLLOW_UP_INTERVAL_MS * 5)
    expect(calls()).toHaveLength(3)
    expect(refreshed).toHaveLength(3)
  })

  it('gives up after the host\'s attempts, counting a failed read as one', async () => {
    let reads = 0
    // A refusal the transport does not retry (a 5xx GET is retried once by the fetch layer), so one read is one request.
    stub.respondWith(() => (++reads % 2 === 0
      ? { status: 422, body: { statusMessage: 'Refused.', data: { code: 'PLUGIN_REPORTED_PROBLEM' } } }
      : { body: { status: 'building' } }))
    mountWithKit(WatchHost)
    await settle()

    follow(UNTIL_LIVE)
    for (let i = 0; i < PLUGIN_FOLLOW_UP_MAX_ATTEMPTS + 5; i += 1) {
      await advance(PLUGIN_FOLLOW_UP_INTERVAL_MS)
    }

    expect(calls()).toHaveLength(PLUGIN_FOLLOW_UP_MAX_ATTEMPTS)
    expect(refreshed).toHaveLength(PLUGIN_FOLLOW_UP_MAX_ATTEMPTS)
  })

  it('adopts its watch under the block id, tells that the work ended, and lets the view go of the watch', async () => {
    let reads = 0
    stub.respondWith(() => ({ body: { status: ++reads < 2 ? 'building' : 'live' } }))
    const settled = vi.fn()
    mountWithKit(WatchHost)
    await settle()

    follow(UNTIL_LIVE, settled)
    expect(adopted.keys).toEqual(['publish'])
    await advance(PLUGIN_FOLLOW_UP_INTERVAL_MS)
    expect(adopted.released).toBe(0)
    expect(settled).not.toHaveBeenCalled()

    await advance(PLUGIN_FOLLOW_UP_INTERVAL_MS)
    expect(settled).toHaveBeenCalledTimes(1)
    expect(adopted.released).toBe(1)
    expect(refreshed).toHaveLength(2)
  })

  it('lets the view go of its watch when the route cannot be read, and refreshes nothing', async () => {
    mountWithKit(WatchHost)
    await settle()

    // A follow-up whose condition names a route with a parameter the page does not hold is unavailable to read.
    follow({ refresh: ['status'], until: { ...UNTIL_LIVE.until, route: { method: 'get', path: '/sites/{missing}' } } })
    expect(adopted.keys).toEqual(['publish'])
    await advance(PLUGIN_FOLLOW_UP_INTERVAL_MS)

    expect(adopted.released).toBe(1)
    expect(refreshed).toHaveLength(0)
    await advance(PLUGIN_FOLLOW_UP_INTERVAL_MS * 3)
    expect(calls()).toHaveLength(0)
  })

  it('does not say the work ended when it gave up', async () => {
    stub.respondWith(() => ({ body: { status: 'building' } }))
    const settled = vi.fn()
    mountWithKit(WatchHost)
    await settle()

    follow(UNTIL_LIVE, settled)
    for (let i = 0; i < PLUGIN_FOLLOW_UP_MAX_ATTEMPTS + 2; i += 1) {
      await advance(PLUGIN_FOLLOW_UP_INTERVAL_MS)
    }

    expect(calls()).toHaveLength(PLUGIN_FOLLOW_UP_MAX_ATTEMPTS)
    expect(settled).not.toHaveBeenCalled()
  })

  it('starting again cancels the running follow-up, and nothing starts for none or a condition that is not a GET', async () => {
    stub.respondWith(() => ({ body: { status: 'building' } }))
    mountWithKit(WatchHost)
    await settle()

    follow(UNTIL_LIVE)
    await advance(PLUGIN_FOLLOW_UP_INTERVAL_MS / 2)
    follow(UNTIL_LIVE)
    await advance(PLUGIN_FOLLOW_UP_INTERVAL_MS / 2)
    expect(calls()).toHaveLength(0)
    await advance(PLUGIN_FOLLOW_UP_INTERVAL_MS / 2)
    expect(calls()).toHaveLength(1)

    follow(undefined)
    await advance(PLUGIN_FOLLOW_UP_INTERVAL_MS * 3)
    follow({ ...UNTIL_LIVE, until: { ...LIVE, route: { method: 'post', path: '/sites/{siteId}' } } })
    await advance(PLUGIN_FOLLOW_UP_INTERVAL_MS * 3)
    expect(calls()).toHaveLength(1)
  })
})

/** Whether the reader that mounts after the others is on screen. */
const lateReader = ref(false)

/** A view that refreshes as the real one does, holding two sources, one condition and, when asked, a late source. */
const SharedHost = defineComponent({
  setup() {
    providePluginView({
      pluginId: 'acme-sites',
      values: { siteId: '7' },
      externalOrigins: [],
      frameOrigins: [],
      refreshes,
      refresh: (ids: string[]) => {
        const next = { ...refreshes.value }
        for (const id of ids) {
          next[id] = (next[id] ?? 0) + 1
        }
        refreshes.value = next
      },
      open: () => undefined,
      adopt: recordAdoption,
      track: () => undefined,
    })
    return () => [h(Reader), h(Reader), h(Watcher), lateReader.value ? h(Reader) : null]
  },
})

/**
 * The reads of the site route the stub received.
 *
 * @returns The GET requests, oldest first.
 */
const siteReads = (): string[] => stub.requests.filter(request => request.method === 'GET' && request.url.includes('/routes/')).map(request => request.url)

describe('reads shared inside a view', () => {
  afterEach(() => {
    vi.useRealTimers()
  })

  beforeEach(() => {
    condition.value = LIVE
    watched.value = ['status']
    lateReader.value = false
    stub.respondWith(() => ({ body: { status: 'building' } }))
  })

  it('sends one request for the sources and the condition that name the same route', async () => {
    mountWithKit(SharedHost)
    await settle()

    expect(siteReads()).toHaveLength(1)
  })

  it('sends one new request for all of them when the view is refreshed, and one for each query', async () => {
    mountWithKit(SharedHost)
    await settle()

    refreshes.value = { status: 1 }
    await nextTick()
    await settle()
    expect(siteReads()).toHaveLength(2)

    query.value = { page: '2' }
    await nextTick()
    await settle()
    // The two sources that read with the new query share the one request; the condition has no query and reads nothing.
    expect(siteReads()).toHaveLength(3)
    expect(siteReads()[2]).toContain('?page=2')
  })

  it('answers a block that mounts later from the same read until the view is refreshed', async () => {
    mountWithKit(SharedHost)
    await settle()

    lateReader.value = true
    await nextTick()
    await settle()

    expect(siteReads()).toHaveLength(1)
  })

  it('asks again after a call that may have changed the plugin\'s data ended', async () => {
    mountWithKit(SharedHost)
    await settle()
    await usePluginsStore().callRoute('acme-sites', { method: 'post', path: '/sites/{siteId}/publish' }, { siteId: '7' })

    lateReader.value = true
    await nextTick()
    await settle()

    expect(siteReads()).toHaveLength(2)
  })

  it('answers a block that mounts within the age limit from the picture, and one that mounts after it with a new read', async () => {
    vi.useFakeTimers({ toFake: ['Date'] })
    mountWithKit(SharedHost)
    await settle()

    vi.advanceTimersByTime(PLUGIN_VIEW_READ_MAX_AGE_MS - 1)
    lateReader.value = true
    await nextTick()
    await settle()
    expect(siteReads()).toHaveLength(1)

    lateReader.value = false
    await nextTick()
    vi.advanceTimersByTime(1)
    lateReader.value = true
    await nextTick()
    await settle()
    expect(siteReads()).toHaveLength(2)
  })

  it('does not keep a read that failed, so the next block tries again', async () => {
    let reads = 0
    stub.respondWith(() => (++reads === 1
      ? { status: 422, body: { statusMessage: 'Refused.', data: { code: 'PLUGIN_REPORTED_PROBLEM' } } }
      : { body: { status: 'building' } }))
    mountWithKit(SharedHost)
    await settle()
    expect(siteReads()).toHaveLength(1)

    lateReader.value = true
    await nextTick()
    await settle()

    expect(siteReads()).toHaveLength(2)
  })

  it('shares nothing with another view of the same page', async () => {
    mountWithKit(SharedHost).unmount()
    await settle()
    mountWithKit(SharedHost)
    await settle()

    expect(siteReads()).toHaveLength(2)
  })

  it('gives the blocks a follow-up refreshes the answer the follow-up read, so the poll costs one request', async () => {
    vi.useFakeTimers({ toFake: ['setTimeout', 'clearTimeout'] })
    stub.respondWith(() => ({ body: { status: 'live' } }))
    mountWithKit(SharedHost)
    await settle()
    expect(siteReads()).toHaveLength(1)

    follow(UNTIL_LIVE)
    await advance(PLUGIN_FOLLOW_UP_INTERVAL_MS)

    // The follow-up's own read, and nothing more: the sources and the condition took its answer.
    expect(siteReads()).toHaveLength(2)
    expect(refreshes.value).toEqual({ status: 1 })
  })
})
