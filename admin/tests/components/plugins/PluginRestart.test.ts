/**
 * Unit tests for PluginRestart: the banner shows only while a restart is pending, the button opens a confirmation
 * that restarts nothing until it is confirmed, and a confirmed restart posts the command and opens the waiting dialog.
 */
import { afterEach, describe, expect, it, vi } from 'vitest'
import { createPinia, setActivePinia } from 'pinia'
import { flushPromises, mount } from '@vue/test-utils'
import PluginRestart from '@/components/plugins/PluginRestart.vue'
import { usePluginsStore } from '@/stores/plugins'
import { PluginRestartState } from '@/types/plugin'
import { json, problem } from '../../helpers/fetch'
import { createTestI18n } from '../../helpers/i18n'

/**
 * Mounts the component over a store, with the teleport inlined.
 *
 * @param requiresRestart - Whether the store says a restart is pending.
 * @returns The wrapper and the store.
 */
const mountRestart = (requiresRestart: boolean) => {
  const pinia = createPinia()
  setActivePinia(pinia)
  const store = usePluginsStore()
  store.requiresRestart = requiresRestart
  const wrapper = mount(PluginRestart, { global: { plugins: [createTestI18n(), pinia], stubs: { teleport: true } } })
  return { wrapper, store }
}

/**
 * The answer of the liveness probe for a process that started at a fixed instant.
 *
 * @returns The response.
 */
const probe = (): Response => json({ status: 'Healthy', timestamp: 't', processStartedAt: 'A' })

afterEach(() => {
  vi.useRealTimers()
  vi.unstubAllGlobals()
})

describe('PluginRestart', () => {
  it('shows no banner while nothing is pending', () => {
    const { wrapper } = mountRestart(false)

    expect(wrapper.find('[data-testid="plugins-restart-banner"]').exists()).toBe(false)
  })

  it('asks for a confirmation first and restarts nothing when it is dismissed', async () => {
    const mock = vi.fn()
    vi.stubGlobal('fetch', mock)
    const { wrapper } = mountRestart(true)

    await wrapper.get('[data-testid="plugins-restart"]').trigger('click')
    expect(wrapper.find('[data-testid="plugins-restart-confirm"]').exists()).toBe(true)
    await wrapper.get('[data-testid="dialog-cancel"]').trigger('click')

    expect(wrapper.find('[data-testid="plugins-restart-confirm"]').exists()).toBe(false)
    expect(mock).not.toHaveBeenCalled()
  })

  it('posts the restart after the confirmation and opens the waiting dialog', async () => {
    vi.useFakeTimers()
    const mock = vi.fn(async (url: string) => (url === '/health' ? probe() : new Response(null, { status: 202 })))
    vi.stubGlobal('fetch', mock)
    const { wrapper, store } = mountRestart(true)

    await wrapper.get('[data-testid="plugins-restart"]').trigger('click')
    await wrapper.get('[data-testid="dialog-confirm"]').trigger('click')
    await vi.advanceTimersByTimeAsync(0)

    expect(mock.mock.calls.some(([url]) => String(url) === '/api/admin/plugins/restart')).toBe(true)
    expect(store.restartState).toBe(PluginRestartState.Restarting)
    expect(wrapper.find('[data-testid="plugin-restart-waiting"]').exists()).toBe(true)
    wrapper.unmount()
  })

  it('shows the server sentence when the restart is refused', async () => {
    vi.stubGlobal('fetch', vi.fn(async (url: string) => (url === '/health' ? probe() : problem('Not allowed.', 403))))
    const { wrapper } = mountRestart(true)

    await wrapper.get('[data-testid="plugins-restart"]').trigger('click')
    await wrapper.get('[data-testid="dialog-confirm"]').trigger('click')
    await flushPromises()

    expect(wrapper.get('[data-testid="plugins-restart-error"]').text()).toContain('Not allowed.')
    expect(wrapper.find('[data-testid="plugin-restart-dialog"]').exists()).toBe(false)
  })
})
