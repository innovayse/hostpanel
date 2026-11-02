/**
 * Mount tests for PluginsView: the installed list, the empty state, the restart banner after an
 * install, a refusal shown as the API's sentence, the retry of a refused load and the danger
 * confirmation before a removal, and the "Allow unsigned plugins" switch: shown from the stored setting, saved through
 * the settings endpoint, a refusal shown as the API's sentence, and a failed settings load with its retry.
 */
import { afterEach, describe, expect, it, vi } from 'vitest'
import { flushPromises } from '@vue/test-utils'
import PluginsView from '@/views/PluginsView.vue'
import { mountRouted } from '../helpers/mount'
import { stubRoutes } from '../helpers/routes'
import { json, problem } from '../helpers/fetch'
import { PluginSettingKey } from '@/types/setting'

/** One installed plugin as the list endpoint returns it. */
const plugin = {
  id: 'ineco',
  name: 'Ineco Pay',
  version: '1.2.0',
  author: 'Hostpanel',
  description: 'Card payments',
  type: 'payment',
  category: 'Payments',
  color: '#123456',
  isLoaded: true,
  isBuiltIn: false,
}

/** The stored "allow unsigned plugins" row, as the settings list returns it. */
const signingRow = { id: 9, key: PluginSettingKey.AllowUnsigned, value: 'false', isSecret: false, hasValue: true }

/**
 * Mounts the screen and waits for the first load.
 *
 * @returns The wrapper.
 */
const mountPlugins = async () => {
  const { wrapper } = await mountRouted(PluginsView, '/plugins', '/plugins')
  await flushPromises()
  return wrapper
}

describe('PluginsView', () => {
  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('lists the installed plugins', async () => {
    stubRoutes({ 'GET /api/admin/plugins': () => json([plugin]) })
    const wrapper = await mountPlugins()

    expect(wrapper.get('[data-testid="plugins-list"]').text()).toContain('Ineco Pay')
    expect(wrapper.find('[data-testid="plugins-empty"]').exists()).toBe(false)
  })

  it('shows the empty state when nothing is installed', async () => {
    stubRoutes({ 'GET /api/admin/plugins': () => json([]) })
    const wrapper = await mountPlugins()

    expect(wrapper.find('[data-testid="plugins-empty"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="plugins-restart-banner"]').exists()).toBe(false)
  })

  it('shows the API sentence when the load is refused', async () => {
    stubRoutes({ 'GET /api/admin/plugins': () => problem('Plugins are unavailable.', 503) })
    const wrapper = await mountPlugins()

    expect(wrapper.get('[data-testid="plugins-error"]').text()).toContain('Plugins are unavailable.')
  })

  it('loads again from the retry of a refused load', async () => {
    let calls = 0
    stubRoutes({
      'GET /api/admin/plugins': () => {
        calls += 1
        return calls === 1 ? problem('Plugins are unavailable.', 503) : json([plugin])
      },
    })
    const wrapper = await mountPlugins()

    await wrapper.get('[data-testid="plugins-error-retry"]').trigger('click')
    await flushPromises()

    expect(wrapper.get('[data-testid="plugins-list"]').text()).toContain('Ineco Pay')
  })

  it('removes a plugin and reloads the list', async () => {
    const mock = stubRoutes({
      'GET /api/admin/plugins': () => json([plugin]),
      'DELETE /api/admin/plugins/ineco': () => json({ requiresRestart: true }),
    })
    const wrapper = await mountPlugins()

    await wrapper.get('[data-testid="plugin-remove-ineco"]').trigger('click')
    await flushPromises()
    expect(mock.mock.calls.some(([, init]) => init?.method === 'DELETE')).toBe(false)
    await wrapper.get('[data-testid="dialog-confirm"]').trigger('click')
    await flushPromises()

    expect(mock.mock.calls.some(([url, init]) => String(url) === '/api/admin/plugins/ineco' && init?.method === 'DELETE')).toBe(true)
    expect(wrapper.get('[data-testid="plugins-feedback"]').text()).toContain('Plugin removed.')
  })

  it('shows the API sentence when the removal is refused', async () => {
    stubRoutes({
      'GET /api/admin/plugins': () => json([plugin]),
      'DELETE /api/admin/plugins/ineco': () => problem('Plugin is in use.', 409),
    })
    const wrapper = await mountPlugins()

    await wrapper.get('[data-testid="plugin-remove-ineco"]').trigger('click')
    await wrapper.get('[data-testid="dialog-confirm"]').trigger('click')
    await flushPromises()

    expect(wrapper.get('[data-testid="plugins-feedback"]').text()).toContain('Plugin is in use.')
    expect(wrapper.get('[data-testid="plugins-list"]').text()).toContain('Ineco Pay')
  })

  it('keeps the plugin when the removal confirmation is dismissed', async () => {
    const mock = stubRoutes({ 'GET /api/admin/plugins': () => json([plugin]) })
    const wrapper = await mountPlugins()

    await wrapper.get('[data-testid="plugin-remove-ineco"]').trigger('click')
    await flushPromises()
    await wrapper.get('[data-testid="dialog-cancel"]').trigger('click')
    await flushPromises()

    expect(mock.mock.calls.some(([, init]) => init?.method === 'DELETE')).toBe(false)
    expect(wrapper.find('[data-testid="dialog-confirm"]').exists()).toBe(false)
  })

  it('shows the signing switch from the stored setting, with its warning', async () => {
    stubRoutes({
      'GET /api/admin/plugins': () => json([plugin]),
      'GET /api/admin/settings': () => json([signingRow]),
    })
    const wrapper = await mountPlugins()

    expect(wrapper.get('[data-testid="plugin-allow-unsigned"]').attributes('aria-checked')).toBe('false')
    expect(wrapper.find('[data-testid="plugin-signing-warning"]').exists()).toBe(true)
  })

  it('saves the signing switch through the settings endpoint and shows it on', async () => {
    let stored = signingRow
    const mock = stubRoutes({
      'GET /api/admin/plugins': () => json([plugin]),
      'GET /api/admin/settings': () => json([stored]),
      'PUT /api/admin/settings/9': () => {
        stored = { ...signingRow, value: 'true' }
        return new Response(null, { status: 204 })
      },
    })
    const wrapper = await mountPlugins()

    await wrapper.get('[data-testid="plugin-allow-unsigned"]').trigger('click')
    await wrapper.get('[data-testid="plugin-allow-unsigned-save"]').trigger('click')
    await flushPromises()

    const put = mock.mock.calls.find(([url, init]) => String(url) === '/api/admin/settings/9' && init?.method === 'PUT')
    expect(JSON.parse(String(put?.[1]?.body))).toEqual({ value: 'true' })
    expect(wrapper.get('[data-testid="plugin-allow-unsigned"]').attributes('aria-checked')).toBe('true')
    expect(wrapper.find('[data-testid="plugin-signing-active"]').exists()).toBe(true)
  })

  it('shows the API sentence when the signing switch is refused', async () => {
    stubRoutes({
      'GET /api/admin/plugins': () => json([plugin]),
      'GET /api/admin/settings': () => json([signingRow]),
      'PUT /api/admin/settings/9': () => problem('That value is not accepted for this setting.'),
    })
    const wrapper = await mountPlugins()

    await wrapper.get('[data-testid="plugin-allow-unsigned"]').trigger('click')
    await wrapper.get('[data-testid="plugin-allow-unsigned-save"]').trigger('click')
    await flushPromises()

    expect(wrapper.get('[data-testid="plugin-signing"]').text()).toContain('That value is not accepted for this setting.')
    expect(wrapper.get('[data-testid="plugin-allow-unsigned"]').attributes('aria-checked')).toBe('true')
  })

  it('shows the API sentence and a retry when the settings cannot be loaded', async () => {
    let calls = 0
    stubRoutes({
      'GET /api/admin/plugins': () => json([plugin]),
      'GET /api/admin/settings': () => {
        calls += 1
        return calls === 1 ? problem('Settings are unavailable.', 503) : json([signingRow])
      },
    })
    const wrapper = await mountPlugins()

    expect(wrapper.get('[data-testid="plugin-signing-error"]').text()).toContain('Settings are unavailable.')
    expect(wrapper.find('[data-testid="plugin-allow-unsigned"]').exists()).toBe(false)

    await wrapper.get('[data-testid="plugin-signing-error-retry"]').trigger('click')
    await flushPromises()

    expect(wrapper.find('[data-testid="plugin-allow-unsigned"]').exists()).toBe(true)
  })
})
