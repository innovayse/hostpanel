/**
 * Mount tests for PluginsView: the installed list, the empty state, the restart banner after an
 * install, a refusal shown as the API's sentence, the retry of a refused load and the danger
 * confirmation before a removal, and the "Allow unsigned plugins" switch: shown from the stored setting, saved through
 * the settings endpoint, a refusal shown as the API's sentence, and a failed settings load with its retry.
 */
import { afterEach, describe, expect, it, vi } from 'vitest'
import { flushPromises } from '@vue/test-utils'
import PluginsView from '@/views/PluginsView.vue'
import PluginUploader from '@/components/plugins/PluginUploader.vue'
import { mountRouted } from '../helpers/mount'
import { stubRoutes as stubAllRoutes } from '../helpers/routes'
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
  status: 'Loaded',
  refusedRule: null,
  refusedDetail: null,
  stagedVersion: null,
}

/**
 * Stubs the routes a test names plus an empty slot widget list for the plugin: routes match by prefix, so
 * without it the widget request would be answered by the plugin list route.
 *
 * @param routes - Answers keyed by the start of `METHOD url`.
 * @returns The fetch mock, for asserting the requests made.
 */
const stubRoutes = (routes: Record<string, () => Response>) =>
  stubAllRoutes({ 'GET /api/admin/plugins/ineco/slot-widgets': () => json([]), ...routes })

/** The stored "allow unsigned plugins" row, as the settings list returns it. */
const signingRow = { id: 9, key: PluginSettingKey.AllowUnsigned, value: 'false' }

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

  it('lists a refused plugin with its reason and offers no enable action', async () => {
    stubRoutes({
      'GET /api/admin/plugins': () => json([
        { ...plugin, isLoaded: false, status: 'Refused', refusedRule: 'LabelKeyMissing', refusedDetail: 'menu.title' },
      ]),
    })
    const wrapper = await mountPlugins()

    const card = wrapper.get('[data-testid="plugin-card-ineco"]')
    expect(card.text()).toContain('Refused')
    expect(card.text()).toContain('A text key used by the plugin is missing from its English texts.')
    expect(card.text()).not.toContain('Active')
    expect(wrapper.find('[data-testid="plugin-remove-ineco"]').exists()).toBe(true)
  })

  it('says in the removal prompt that the plugin data is kept', async () => {
    stubRoutes({ 'GET /api/admin/plugins': () => json([plugin]) })
    const wrapper = await mountPlugins()

    await wrapper.get('[data-testid="plugin-remove-ineco"]').trigger('click')
    await flushPromises()

    expect(wrapper.text()).toContain('Its settings and stored data are kept')
  })

  it('purges the kept data of a removed plugin behind a confirmation', async () => {
    // The list follows the server: installed, then removed with its data kept, then gone after the purge.
    let rows: unknown[] = [plugin]
    const mock = stubRoutes({
      'GET /api/admin/plugins': () => json(rows),
      'DELETE /api/admin/plugins/ineco/data': () => {
        rows = []
        return json({ requiresRestart: false })
      },
      'DELETE /api/admin/plugins/ineco': () => {
        rows = [{ ...plugin, version: '', isLoaded: false, status: 'Removed' }]
        return json({ requiresRestart: true })
      },
    })
    const wrapper = await mountPlugins()
    await wrapper.get('[data-testid="plugin-remove-ineco"]').trigger('click')
    await wrapper.get('[data-testid="dialog-confirm"]').trigger('click')
    await flushPromises()

    await wrapper.get('[data-testid="plugin-purge-ineco"]').trigger('click')
    await flushPromises()
    const purgeCalls = () => mock.mock.calls.filter(([url]) => String(url) === '/api/admin/plugins/ineco/data')
    expect(purgeCalls()).toHaveLength(0)
    await wrapper.get('[data-testid="dialog-confirm"]').trigger('click')
    await flushPromises()

    expect(purgeCalls()).toHaveLength(1)
    expect(wrapper.get('[data-testid="plugins-feedback"]').text()).toContain('Plugin data purged.')
    expect(wrapper.find('[data-testid="plugins-kept-data"]').exists()).toBe(false)
  })

  it('says a newer upload is staged and shows the restart banner', async () => {
    stubRoutes({
      'GET /api/admin/plugins': () => json([plugin]),
      'POST /api/admin/plugins/install': () => json({ requiresRestart: true, stagedVersion: '1.3.0' }),
    })
    const wrapper = await mountPlugins()

    wrapper.getComponent(PluginUploader).vm.$emit('upload', new File(['z'], 'new.zip'))
    await flushPromises()

    expect(wrapper.get('[data-testid="plugins-feedback"]').text()).toContain('Version 1.3.0 uploaded and staged.')
    expect(wrapper.find('[data-testid="plugins-restart-banner"]').exists()).toBe(true)
  })

  it('shows the downgrade refusal and forces the older version after a confirmation', async () => {
    const mock = stubRoutes({
      'GET /api/admin/plugins': () => json([plugin]),
      'POST /api/admin/plugins/install?force=true': () => json({ requiresRestart: true }),
      'POST /api/admin/plugins/install': () => json(
        { type: 'PLUGIN_DOWNGRADE_REFUSED', title: 'Refused', status: 409, detail: 'Version 1.0.0 is older than 1.2.0.' },
        409,
      ),
    })
    const wrapper = await mountPlugins()

    wrapper.getComponent(PluginUploader).vm.$emit('upload', new File(['z'], 'old.zip'))
    await flushPromises()

    expect(wrapper.get('[data-testid="plugins-feedback"]').text()).toContain('Version 1.0.0 is older than 1.2.0.')
    expect(wrapper.find('[data-testid="plugins-restart-banner"]').exists()).toBe(false)
    await wrapper.get('[data-testid="dialog-confirm"]').trigger('click')
    await flushPromises()

    expect(mock.mock.calls.some(([url]) => String(url) === '/api/admin/plugins/install?force=true')).toBe(true)
    expect(wrapper.find('[data-testid="plugins-restart-banner"]').exists()).toBe(true)
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

  it('lists the slot widgets of a loaded plugin and saves a binding', async () => {
    const widget = { slotId: 'admin.service.panels', widgetId: 'usage', title: 'Usage', manifestProductIds: [], productIds: [3], isBound: true }
    const mock = stubRoutes({
      'PUT /api/admin/plugins/ineco/slot-widgets/admin.service.panels/usage': () => json({ ...widget }),
      'GET /api/admin/plugins/ineco/slot-widgets': () => json([widget]),
      'GET /api/products': () => json([{ id: 3, name: 'Shared' }, { id: 4, name: 'Reseller' }]),
      'GET /api/admin/plugins': () => json([plugin]),
      'GET /api/admin/settings': () => json([signingRow]),
    })
    const wrapper = await mountPlugins()

    expect(wrapper.get('[data-testid="plugin-widget-chosen"]').text()).toContain('Shared')

    await wrapper.get('[data-testid="plugin-widget-remove-3"]').trigger('click')
    await wrapper.get('[data-testid="plugin-widget-save"]').trigger('click')
    await flushPromises()

    const put = mock.mock.calls.find(([, init]) => init?.method === 'PUT')
    expect(JSON.parse(String(put?.[1]?.body))).toEqual({ productIds: [] })
  })

  it('shows the API sentence when the slot widgets cannot be loaded', async () => {
    stubRoutes({
      'GET /api/admin/plugins/ineco/slot-widgets': () => problem('Widgets are unavailable.', 503),
      'GET /api/admin/plugins': () => json([plugin]),
      'GET /api/admin/settings': () => json([signingRow]),
    })
    const wrapper = await mountPlugins()

    expect(wrapper.get('[data-testid="plugin-slot-widgets-error"]').text()).toContain('Widgets are unavailable.')
  })
})
