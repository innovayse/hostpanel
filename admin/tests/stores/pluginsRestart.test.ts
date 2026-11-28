/**
 * Tests for the plugins store restart flow: the baseline read before the request, polling the health probe until the
 * server answers as a new process (a down server counts as still waiting), the plugin list read afterwards, the
 * timeout and the retry against the same baseline, a refused request, and cancelling the wait. Timers are fake, the
 * transport is the real `useApi` with `fetch` stubbed per path.
 */

import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { createPinia, setActivePinia } from 'pinia'
import { POLL_INTERVALS_MS, RESTART_TIMEOUT_MS } from '@/lib/limits'
import { usePluginsStore } from '@/stores/plugins'
import { PluginChangeResult, PluginRestartState } from '@/types/plugin'
import type { PluginListItemDto } from '@/types/plugin'
import { PollKind } from '@/types/poller'
import { changeStream, json, problem, stubFetch } from '../helpers/fetch'

/** How often the restart wait asks the server, in milliseconds. */
const RESTART_POLL_MS = POLL_INTERVALS_MS[PollKind.ServerRestart]

/** One scripted answer of the health probe: a response, or an error for a server that is down. */
type HealthAnswer = Response | Error

/**
 * Builds the probe's answer for a process that started at a given instant.
 *
 * @param startedAt - The process start instant.
 * @returns The response.
 */
const health = (startedAt: string): Response => json({ status: 'Healthy', timestamp: '2026-11-24T10:00:00Z', processStartedAt: startedAt })

/** What a down server looks like to `fetch`. */
const down = (): Error => new TypeError('Failed to fetch')

/**
 * Builds a listed plugin with only the fields the restart flow reads.
 *
 * @param fields - Fields to set on top of a loaded, running plugin.
 * @returns The list row.
 */
const listed = (fields: Partial<PluginListItemDto>): PluginListItemDto =>
  ({ id: 'example-pay', name: 'Example Pay', version: '1.0.0', status: 'Loaded', isLoaded: true, stagedVersion: null, ...fields }) as PluginListItemDto

/** The scripted server: what the probe answers next, the restart request's answer and the list after the restart. */
interface ScriptedServer {
  /** Answers of the probe, handed out in order; the last one repeats. */
  health: HealthAnswer[]
  /** Answer of POST /api/admin/plugins/restart, or an error for a dropped connection. */
  restart: Response | Error
  /** The plugin list the server holds after the restart. */
  list: PluginListItemDto[]
}

/**
 * Stubs `fetch` per path from the script.
 *
 * @param server - The script; its `health` array can be replaced while the test runs.
 * @returns The mock, for counting calls per path.
 */
const stubServer = (server: ScriptedServer): ReturnType<typeof vi.fn> => {
  let probes = 0
  const mock = vi.fn(async (url: string) => {
    if (url === '/health') {
      const answer = server.health[Math.min(probes, server.health.length - 1)] as HealthAnswer
      probes += 1
      if (answer instanceof Error) {
        throw answer
      }
      return answer.clone()
    }
    if (url === '/api/admin/plugins/restart') {
      if (server.restart instanceof Error) {
        throw server.restart
      }
      return server.restart.clone()
    }
    return json(server.list)
  })
  vi.stubGlobal('fetch', mock)
  return mock
}

/**
 * Counts the calls the mock saw for one path.
 *
 * @param mock - The fetch mock.
 * @param path - The request URL.
 * @returns How many times it was called.
 */
const callsTo = (mock: ReturnType<typeof vi.fn>, path: string): number =>
  mock.mock.calls.filter(call => String(call[0]) === path).length

/**
 * Puts the tab in the background the way the browser does: the property changes and the event fires.
 *
 * @returns Nothing.
 */
const hideTab = (): void => {
  Object.defineProperty(document, 'visibilityState', { configurable: true, get: () => 'hidden' })
  document.dispatchEvent(new Event('visibilitychange'))
}

beforeEach(() => {
  setActivePinia(createPinia())
  vi.useFakeTimers()
})

afterEach(() => {
  Reflect.deleteProperty(document, 'visibilityState')
  vi.useRealTimers()
  vi.unstubAllGlobals()
})

describe('pluginsStore.followRestart', () => {
  it('waits for a restart the host started, without posting a restart, treating a draining 503 as still waiting', async () => {
    const mock = stubServer({
      health: [problem('Draining.', 503), problem('Draining.', 503), down(), health('B')],
      restart: changeStream({}),
      list: [listed({ version: '2.0.0' })],
    })
    const store = usePluginsStore()
    store.plugins = [listed({ version: '1.0.0', isLoaded: false, stagedVersion: '2.0.0' })]

    const done = store.followRestart()
    await vi.advanceTimersByTimeAsync(0)
    expect(store.restartState).toBe(PluginRestartState.Restarting)
    await vi.advanceTimersByTimeAsync(RESTART_POLL_MS * 3)
    await done

    expect(callsTo(mock, '/api/admin/plugins/restart')).toBe(0)
    expect(store.restartState).toBe(PluginRestartState.Running)
    expect(store.restartedPlugins.map(p => p.version)).toEqual(['2.0.0'])
  })

  it('separates a plugin whose change failed from the ones that run', async () => {
    const failed = listed({
      id: 'broken',
      name: 'Broken',
      version: '1.0.0',
      lastChange: { pluginId: 'broken', fromVersion: '1.0.0', toVersion: '2.0.0', result: PluginChangeResult.Failed, error: 'Missing dependency.', restoredVersion: '1.0.0', at: 't' },
    })
    stubServer({ health: [health('A'), health('B')], restart: changeStream({}), list: [listed({ version: '2.0.0' }), failed] })
    const store = usePluginsStore()
    store.plugins = [listed({ isLoaded: false, stagedVersion: '2.0.0' }), listed({ id: 'broken', name: 'Broken', isLoaded: false, stagedVersion: '2.0.0' })]

    const done = store.followRestart()
    await vi.advanceTimersByTimeAsync(RESTART_POLL_MS)
    await done

    expect(store.restartedPlugins.map(p => p.id)).toEqual(['example-pay'])
    expect(store.restartFailures.map(p => p.id)).toEqual(['broken'])
  })

  it('does not raise the banner for an answer the host restarts for, and raises it otherwise', async () => {
    stubFetch(changeStream({ requiresRestart: true, stagedVersion: null, reloaded: false, restarting: true }), json([]))
    const store = usePluginsStore()

    await store.install(new File(['z'], 'plugin.zip'))
    expect(store.restarting).toBe(true)
    expect(store.requiresRestart).toBe(false)

    stubFetch(changeStream({ requiresRestart: true, stagedVersion: null, reloaded: false, restarting: false }), json([]))
    await store.install(new File(['z'], 'plugin.zip'))
    expect(store.restarting).toBe(false)
    expect(store.requiresRestart).toBe(true)
  })
})

describe('pluginsStore.restart', () => {
  it('waits through the old process and a down server, then reads the list and ends Running', async () => {
    stubServer({
      health: [health('A'), health('A'), down(), health('B')],
      restart: changeStream({}),
      list: [listed({ version: '2.0.0' })],
    })
    const store = usePluginsStore()
    store.plugins = [listed({ version: '1.0.0', isLoaded: false, stagedVersion: '2.0.0' })]
    store.requiresRestart = true

    const done = store.restart()
    await vi.advanceTimersByTimeAsync(0)
    expect(store.restartState).toBe(PluginRestartState.Restarting)

    await vi.advanceTimersByTimeAsync(RESTART_POLL_MS)
    expect(store.restartState).toBe(PluginRestartState.Restarting)
    await vi.advanceTimersByTimeAsync(RESTART_POLL_MS)
    expect(store.restartState).toBe(PluginRestartState.Restarting)
    await vi.advanceTimersByTimeAsync(RESTART_POLL_MS)
    await done

    expect(store.restartState).toBe(PluginRestartState.Running)
    expect(store.requiresRestart).toBe(false)
    expect(store.restartedPlugins.map(p => `${p.name} ${p.version}`)).toEqual(['Example Pay 2.0.0'])
  })

  it('treats a dropped connection on the request itself as the server going down', async () => {
    const mock = stubServer({ health: [health('A'), health('B')], restart: down(), list: [listed({})] })
    const store = usePluginsStore()

    const done = store.restart()
    await vi.advanceTimersByTimeAsync(RESTART_POLL_MS)
    await done

    expect(callsTo(mock, '/api/admin/plugins/restart')).toBe(1)
    expect(store.restartState).toBe(PluginRestartState.Running)
    expect(store.restartError).toBeNull()
  })

  it('ends Idle with the sentence when the server refuses the request, and never polls', async () => {
    const mock = stubServer({ health: [health('A')], restart: problem('Not allowed.', 403), list: [] })
    const store = usePluginsStore()
    store.requiresRestart = true

    await store.restart()

    expect(store.restartState).toBe(PluginRestartState.Idle)
    expect(store.restartError).toBe('Not allowed.')
    expect(store.requiresRestart).toBe(true)
    expect(callsTo(mock, '/health')).toBe(1)
  })

  it('times out when the server never answers as a new process, and a retry waits again without restarting again', async () => {
    const server: ScriptedServer = { health: [health('A')], restart: changeStream({}), list: [listed({})] }
    const mock = stubServer(server)
    const store = usePluginsStore()

    const done = store.restart()
    await vi.advanceTimersByTimeAsync(RESTART_TIMEOUT_MS + RESTART_POLL_MS)
    await done
    expect(store.restartState).toBe(PluginRestartState.TimedOut)

    server.health = [health('B')]
    const retry = store.retryRestartWait()
    expect(store.restartState).toBe(PluginRestartState.Restarting)
    await vi.advanceTimersByTimeAsync(RESTART_POLL_MS)
    await retry

    expect(store.restartState).toBe(PluginRestartState.Running)
    expect(callsTo(mock, '/api/admin/plugins/restart')).toBe(1)
  })

  it('stops polling and returns to Idle when the flow is reset', async () => {
    const mock = stubServer({ health: [health('A')], restart: changeStream({}), list: [] })
    const store = usePluginsStore()

    const done = store.restart()
    await vi.advanceTimersByTimeAsync(RESTART_POLL_MS)
    const probesBefore = callsTo(mock, '/health')
    store.resetRestart()
    await vi.advanceTimersByTimeAsync(RESTART_TIMEOUT_MS)
    await done

    expect(store.restartState).toBe(PluginRestartState.Idle)
    expect(callsTo(mock, '/health')).toBe(probesBefore)
  })

  it('ends a wait on wall clock time although the tab is hidden and its poll is paused', async () => {
    const mock = stubServer({ health: [health('A')], restart: changeStream({}), list: [] })
    const store = usePluginsStore()

    const done = store.restart()
    await vi.advanceTimersByTimeAsync(0)
    const probesBefore = callsTo(mock, '/health')
    hideTab()
    await vi.advanceTimersByTimeAsync(RESTART_TIMEOUT_MS)
    await done

    expect(store.restartState).toBe(PluginRestartState.TimedOut)
    expect(callsTo(mock, '/health')).toBe(probesBefore)
  })

  it('cancels the old wait at once when the flow is reset, without waiting for its next probe', async () => {
    stubServer({ health: [health('A')], restart: changeStream({}), list: [] })
    const store = usePluginsStore()
    let settled = false

    const done = store.restart().then(() => {
      settled = true
    })
    await vi.advanceTimersByTimeAsync(0)
    expect(settled).toBe(false)
    store.resetRestart()
    await vi.advanceTimersByTimeAsync(0)
    await done

    expect(settled).toBe(true)
    expect(store.restartState).toBe(PluginRestartState.Idle)
  })

  it('cancels the old wait at once when a newer restart starts, and leaves its state to the newer one', async () => {
    const mock = stubServer({ health: [health('A')], restart: changeStream({}), list: [] })
    const store = usePluginsStore()
    let firstSettled = false

    const first = store.restart().then(() => {
      firstSettled = true
    })
    await vi.advanceTimersByTimeAsync(0)
    const second = store.restart()
    await vi.advanceTimersByTimeAsync(0)
    expect(firstSettled).toBe(true)
    await first

    expect(store.restartState).toBe(PluginRestartState.Restarting)
    await vi.advanceTimersByTimeAsync(RESTART_TIMEOUT_MS)
    await second
    expect(store.restartState).toBe(PluginRestartState.TimedOut)
    expect(callsTo(mock, '/health')).toBeGreaterThan(0)
  })
})
