/**
 * Unit tests for PluginRestartDialog: it stays closed while no restart runs, cannot be dismissed while the panel waits
 * for the server, lists the plugins that now run with their versions, offers Retry and Close after a timeout, and
 * stops the wait when it goes away.
 */
import { afterEach, describe, expect, it, vi } from 'vitest'
import { createPinia, setActivePinia } from 'pinia'
import { mount } from '@vue/test-utils'
import PluginRestartDialog from '@/components/plugins/PluginRestartDialog.vue'
import { POLL_INTERVALS_MS } from '@/lib/limits'
import { usePluginsStore } from '@/stores/plugins'
import { PluginChangeResult, PluginRestartState } from '@/types/plugin'
import type { PluginListItemDto } from '@/types/plugin'
import { PollKind } from '@/types/poller'
import { json } from '../../helpers/fetch'
import { createTestI18n } from '../../helpers/i18n'

/** How often the restart wait asks the server, in milliseconds. */
const RESTART_POLL_MS = POLL_INTERVALS_MS[PollKind.ServerRestart]

/**
 * Mounts the dialog over a store in a given restart state, with the teleport inlined.
 *
 * @param state - The restart state the store is in.
 * @returns The wrapper and the store.
 */
const mountDialog = (state: PluginRestartState) => {
  const pinia = createPinia()
  setActivePinia(pinia)
  const store = usePluginsStore()
  store.restartState = state
  const wrapper = mount(PluginRestartDialog, { global: { plugins: [createTestI18n(), pinia], stubs: { teleport: true } } })
  return { wrapper, store }
}

/**
 * Builds one listed plugin with only the fields the restart flow reads.
 *
 * @param version - The version the row carries.
 * @param loaded - Whether the plugin is loaded; false stands for one waiting for the restart.
 * @returns The list row.
 */
const listed = (version: string, loaded: boolean): PluginListItemDto =>
  ({ id: 'example-pay', name: 'Example Pay', version, status: 'Loaded', isLoaded: loaded, stagedVersion: null }) as PluginListItemDto

afterEach(() => {
  vi.useRealTimers()
  vi.unstubAllGlobals()
})

describe('PluginRestartDialog', () => {
  it('is closed while no restart runs', () => {
    const { wrapper } = mountDialog(PluginRestartState.Idle)

    expect(wrapper.find('[data-testid="plugin-restart-dialog"]').exists()).toBe(false)
  })

  it('shows a loader and the hint while waiting, and nothing can dismiss it', async () => {
    const { wrapper, store } = mountDialog(PluginRestartState.Restarting)

    expect(wrapper.find('[data-testid="plugin-restart-waiting"]').exists()).toBe(true)
    expect(wrapper.text()).toContain('Restarting')
    expect(wrapper.text()).toContain('The panel will be back in a moment.')
    expect(wrapper.find('[data-testid="dialog-close"]').exists()).toBe(false)
    expect(wrapper.find('[data-testid="plugin-restart-close"]').exists()).toBe(false)
    expect(wrapper.find('[data-testid="plugin-restart-retry"]').exists()).toBe(false)
    await wrapper.get('[role="dialog"]').trigger('keydown', { key: 'Escape' })
    await wrapper.get('[data-testid="dialog-backdrop"]').trigger('click')
    expect(store.restartState).toBe(PluginRestartState.Restarting)
  })

  it('shows Loaded and running with each plugin and its version, and Close ends the flow', async () => {
    vi.useFakeTimers()
    let probes = 0
    // The server answers as the old process once (the baseline), then as a new one; its list now runs version 2.
    vi.stubGlobal('fetch', vi.fn(async (url: string) => {
      if (url === '/health') {
        probes += 1
        return json({ status: 'Healthy', timestamp: 't', processStartedAt: probes === 1 ? 'A' : 'B' })
      }
      return url.endsWith('/restart') ? new Response(null, { status: 202 }) : json([listed('2.0.0', true)])
    }))
    const { wrapper, store } = mountDialog(PluginRestartState.Idle)
    store.plugins = [listed('1.0.0', false)]

    const done = store.restart()
    await vi.advanceTimersByTimeAsync(RESTART_POLL_MS)
    await done

    expect(wrapper.get('[data-testid="plugin-restart-message"]').text()).toContain('Loaded and running')
    expect(wrapper.get('[data-testid="plugin-restart-plugin-example-pay"]').text()).toBe('Example Pay, version 2.0.0')
    await wrapper.get('[data-testid="plugin-restart-close"]').trigger('click')
    expect(store.restartState).toBe(PluginRestartState.Idle)
  })

  it('shows a plugin whose change failed with the reason and the restored version, next to the running ones', async () => {
    vi.useFakeTimers()
    let probes = 0
    const broken = {
      ...listed('1.0.0', true),
      id: 'broken',
      name: 'Broken',
      lastChange: { pluginId: 'broken', fromVersion: '1.0.0', toVersion: '2.0.0', result: PluginChangeResult.Failed, error: 'Missing dependency.', restoredVersion: '1.0.0', at: 't' },
    }
    vi.stubGlobal('fetch', vi.fn(async (url: string) => {
      if (url === '/health') {
        probes += 1
        return json({ status: 'Healthy', timestamp: 't', processStartedAt: probes === 1 ? 'A' : 'B' })
      }
      return json([listed('2.0.0', true), broken])
    }))
    const { wrapper, store } = mountDialog(PluginRestartState.Idle)
    store.plugins = [listed('1.0.0', false), { ...listed('1.0.0', false), id: 'broken', name: 'Broken' }]

    const done = store.followRestart()
    await vi.advanceTimersByTimeAsync(RESTART_POLL_MS)
    await done

    expect(wrapper.get('[data-testid="plugin-restart-plugin-example-pay"]').text()).toBe('Example Pay, version 2.0.0')
    expect(wrapper.find('[data-testid="plugin-restart-plugin-broken"]').exists()).toBe(false)
    expect(wrapper.get('[data-testid="plugin-restart-failed-broken"]').text()).toBe('Broken failed to load. Update failed: Missing dependency. Version 1.0.0 is still running.')
  })

  it('shows the timeout with Retry and Close, and Retry waits again', async () => {
    vi.useFakeTimers()
    vi.stubGlobal('fetch', vi.fn(async () => json({ status: 'Healthy', timestamp: 't', processStartedAt: 'A' })))
    const { wrapper, store } = mountDialog(PluginRestartState.TimedOut)

    expect(wrapper.get('[data-testid="plugin-restart-message"]').text()).toContain('did not come back in time')
    expect(wrapper.find('[data-testid="plugin-restart-close"]').exists()).toBe(true)
    await wrapper.get('[data-testid="plugin-restart-retry"]').trigger('click')

    expect(store.restartState).toBe(PluginRestartState.Restarting)
    wrapper.unmount()
  })

  it('stops the wait when it goes away', () => {
    const { wrapper, store } = mountDialog(PluginRestartState.Restarting)

    wrapper.unmount()

    expect(store.restartState).toBe(PluginRestartState.Idle)
  })
})
