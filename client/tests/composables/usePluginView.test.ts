/**
 * Tests for `composables/usePluginView.ts`, through the real store, API composable and transport
 * against a stub server: a block outside a view is a host bug; a source is read on mount, on a
 * refresh of its block id and on a change of its query; a refusal shows the API's sentence; and a
 * source the page cannot read calls nothing and says nothing.
 *
 * @module tests/composables/usePluginView.test
 */

import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { defineComponent, h, nextTick, onUnmounted, ref } from 'vue'
import { enableAutoUnmount } from '@vue/test-utils'
import { startHeldServer } from '../helpers/heldServer'
import { startTransport } from '../helpers/stubTransport'
import type { StubServer } from '../helpers/stubServer'
import { mountWithKit } from '~/tests/helpers/mountWithKit'
import { providePluginView, usePluginCondition, usePluginFollowUp, usePluginSource, usePluginView } from '~/composables/usePluginView'
import { PLUGIN_FOLLOW_UP_INTERVAL_MS, PLUGIN_FOLLOW_UP_MAX_ATTEMPTS } from '~/constants/pluginFollowUp'
import type { PluginSource } from '~/types/pluginblock'
import type { PluginFollowUp } from '~/types/pluginfollowup'
import type { PluginViewCondition } from '~/types/pluginviewcondition'
import { settle, until } from '~/tests/helpers/settle'

/** The stub playing the BFF. */
let stub: StubServer

/** The refresh counts the test view hands out. */
const refreshes = ref<Record<string, number>>({})

/** The query the test block reads with. */
const query = ref<Record<string, string>>({})

/** The source the test block reads; tests replace it. */
const source = ref<PluginSource>({ route: { method: 'get', path: '/sites/{siteId}' }, field: 'status' })

/**
 * What a view does with the follow-ups it adopts, as `PluginView` does it: a later watch of the same key ends the
 * earlier one, and leaving the view ends them all. Called in the view's setup.
 *
 * @returns The context's `adopt`.
 */
const keepsWatches = (): ((key: string, stop: () => void) => () => void) => {
  const watches = new Map<string, () => void>()
  onUnmounted(() => {
    for (const stop of watches.values()) {
      stop()
    }
  })
  return (key, stop) => {
    watches.get(key)?.()
    watches.set(key, stop)
    return () => {
      if (watches.get(key) === stop) {
        watches.delete(key)
      }
    }
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
      adopt: keepsWatches(),
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
      await vi.waitFor(() => expect(server.held).toHaveLength(1))
      query.value = { page: '2' }
      await nextTick()
      await vi.waitFor(() => expect(server.held).toHaveLength(2))

      server.held[1]?.answer({ status: 'newer' })
      // Real sockets: the newer answer is awaited until it is shown, so the older one surely lands after it.
      await vi.waitFor(() => expect(wrapper.get('[data-testid="value"]').text()).toBe('newer'))
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

/** Starts the test block's follow-up; set when the block mounts. */
let follow: (followUp: PluginFollowUp | undefined, settled?: () => void) => void = () => undefined

/** Whether the watching block is on the page; a test turns it off to hide the block that started the watch. */
const watcherShown = ref(true)

/** A block that shows `shown` while its condition holds and can start a follow-up. */
const Watcher = defineComponent({
  setup() {
    const visible = usePluginCondition(() => condition.value, () => 'status')
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
      adopt: keepsWatches(),
      track: () => undefined,
    })
    return () => (watcherShown.value ? h(Watcher) : h('span', { 'data-testid': 'gone' }))
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
    watcherShown.value = true
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

  it('stops when the view is left: no further read, and a read still out refreshes nothing', async () => {
    const server = await startHeldServer()
    try {
      const wrapper = mountWithKit(WatchHost)
      follow(UNTIL_LIVE)
      await advance(PLUGIN_FOLLOW_UP_INTERVAL_MS)
      await until(() => server.held.length === 1, 'the first follow-up read')

      wrapper.unmount()
      server.held[0]?.answer({ status: 'building' })
      await settle()
      await advance(PLUGIN_FOLLOW_UP_INTERVAL_MS * 3)

      expect(server.held).toHaveLength(1)
      expect(refreshed).toHaveLength(0)
    } finally {
      await server.close()
    }
  })

  it('goes on when only the block that started it leaves the page, and tells that the work ended', async () => {
    let reads = 0
    stub.respondWith(() => ({ body: { status: ++reads < 3 ? 'building' : 'live' } }))
    const settled = vi.fn()
    mountWithKit(WatchHost)
    await settle()

    follow(UNTIL_LIVE, settled)
    await advance(PLUGIN_FOLLOW_UP_INTERVAL_MS)
    expect(calls()).toHaveLength(1)

    // The block hides itself (a publish button whose own condition stopped holding) while the work still runs.
    watcherShown.value = false
    await nextTick()
    await advance(PLUGIN_FOLLOW_UP_INTERVAL_MS)
    expect(calls()).toHaveLength(2)
    expect(settled).not.toHaveBeenCalled()

    await advance(PLUGIN_FOLLOW_UP_INTERVAL_MS)
    expect(calls()).toHaveLength(3)
    expect(settled).toHaveBeenCalledTimes(1)
    expect(refreshed).toHaveLength(3)

    await advance(PLUGIN_FOLLOW_UP_INTERVAL_MS * 5)
    expect(calls()).toHaveLength(3)
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
