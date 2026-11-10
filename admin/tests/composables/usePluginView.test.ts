/**
 * Tests for the plugin view wiring: a source read again when its block is refreshed, a follow-up kept by
 * the view and ended by it, no follow-up on a condition that is not a GET, and the action message outlet.
 */
import { afterEach, describe, expect, it, vi } from 'vitest'
import { defineComponent, h, ref } from 'vue'
import { flushPromises } from '@vue/test-utils'
import {
  providePluginActionMessages,
  providePluginView,
  usePluginActionMessageOutlet,
  usePluginFollowUp,
  usePluginSource,
  usePluginView,
} from '@/composables/usePluginView'
import type { PluginActionMessage, PluginViewContext } from '@/composables/usePluginView'
import { PLUGIN_FOLLOW_UP_INTERVAL_MS } from '@/lib/pluginView'
import { json } from '../helpers/fetch'
import { mountWithApp } from '../helpers/mount'
import { requestsOf, stubRouteTable } from '../helpers/routes'
import { ext, SAMPLE_PLUGIN } from '../helpers/pluginView'

afterEach(() => {
  vi.useRealTimers()
  vi.unstubAllGlobals()
})

/** What the probe block exposes to the test. */
interface Probe {
  /** The view context. */
  context: PluginViewContext
  /** The value its source read. */
  value: () => unknown
  /** Starts a follow-up. */
  follow: ReturnType<typeof usePluginFollowUp>
}

/**
 * Mounts a probe block, reading `/state` as block `probe`, below a context built like PluginView's, so the
 * test can drive refreshes and see the follow-ups the view keeps.
 *
 * @returns The probe and the stop functions the view holds, by block id.
 */
const mountProbe = async (): Promise<{ probe: Probe; watches: Map<string, () => void> }> => {
  const probe: Partial<Probe> = {}
  const watches = new Map<string, () => void>()
  const Block = defineComponent({
    setup: () => {
      const { value } = usePluginSource(() => ({ route: { method: 'get', path: '/state' }, field: 'n' }), () => 'probe')
      probe.context = usePluginView()
      probe.value = () => value.value
      probe.follow = usePluginFollowUp(() => 'probe')
      return () => null
    },
  })
  const Host = defineComponent({
    setup: () => {
      const refreshes = ref<Record<string, number>>({})
      providePluginView({
        pluginId: SAMPLE_PLUGIN,
        values: {},
        externalOrigins: [],
        refreshes,
        refresh: (ids) => {
          const next = { ...refreshes.value }
          for (const id of ids) {
            next[id] = (next[id] ?? 0) + 1
          }
          refreshes.value = next
        },
        open: () => undefined,
        adopt: (key, stop) => {
          watches.get(key)?.()
          watches.set(key, stop)
          return () => {
            watches.delete(key)
          }
        },
      })
      return () => h(Block)
    },
  })
  mountWithApp(Host)
  await flushPromises()
  return { probe: probe as Probe, watches }
}

describe('usePluginSource', () => {
  it('reads when mounted and again when its block is refreshed', async () => {
    let n = 1
    const mock = stubRouteTable({ [`GET ${ext('state')}`]: () => json({ n: n++ }) })
    const { probe } = await mountProbe()
    expect(probe.value()).toBe(1)

    probe.context.refresh(['probe'])
    await flushPromises()

    expect(probe.value()).toBe(2)
    expect(requestsOf(mock)).toHaveLength(2)
  })
})

describe('usePluginFollowUp', () => {
  it('is kept by the view and reads nothing more once the view ends it', async () => {
    vi.useFakeTimers()
    const mock = stubRouteTable({ [`GET ${ext('state')}`]: () => json({ n: 0 }) })
    const { probe, watches } = await mountProbe()
    const before = requestsOf(mock).length

    probe.follow({ refresh: [], until: { route: { method: 'get', path: '/state' }, field: 'n', equals: 1 } })
    expect(watches.has('probe')).toBe(true)
    watches.get('probe')?.()
    await vi.advanceTimersByTimeAsync(PLUGIN_FOLLOW_UP_INTERVAL_MS * 3)

    expect(requestsOf(mock)).toHaveLength(before)
  })

  it('starts nothing for a condition on a route that is not a GET', async () => {
    stubRouteTable({ [`GET ${ext('state')}`]: () => json({ n: 0 }) })
    const { probe, watches } = await mountProbe()

    probe.follow({ refresh: [], until: { route: { method: 'post', path: '/state' }, field: 'n', equals: 1 } })

    expect(watches.size).toBe(0)
  })
})

describe('action message outlet', () => {
  it('shows the latest sentence and takes down only its own', async () => {
    const seen: { current: PluginActionMessage | null } = { current: null }
    const held: { outlet: ReturnType<typeof usePluginActionMessageOutlet> } = { outlet: null }
    const Child = defineComponent({
      setup: () => {
        held.outlet = usePluginActionMessageOutlet()
        return () => null
      },
    })
    const Parent = defineComponent({
      setup: () => {
        const message = providePluginActionMessages()
        return () => {
          seen.current = message.value
          return h(Child)
        }
      },
    })
    mountWithApp(Parent)

    const takeFirst = held.outlet?.({ text: 'First', failed: false })
    held.outlet?.({ text: 'Second', failed: true })
    takeFirst?.()
    await flushPromises()

    expect(seen.current).toEqual({ text: 'Second', failed: true })
  })

  it('is absent for an action that stands alone', () => {
    let outlet: ReturnType<typeof usePluginActionMessageOutlet> = null
    mountWithApp(defineComponent({
      setup: () => {
        outlet = usePluginActionMessageOutlet()
        return () => null
      },
    }))

    expect(outlet).toBeNull()
  })
})
