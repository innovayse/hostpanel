/**
 * Mount tests for PluginsView: the installed list, the empty state, the restart banner after an
 * install, a refusal shown as the API's sentence, the retry of a refused load and the danger
 * confirmation before a removal, and the "Allow unsigned plugins" switch: shown from the stored setting, saved through
 * the settings endpoint, a refusal shown as the API's sentence, and a failed settings load with its retry.
 * The Browse tab: hidden when the registry is switched off, shown otherwise, with the registry list, an install
 * from it, the restart banner it raises, and the update badge on an installed plugin.
 */
import { afterEach, describe, expect, it, vi } from 'vitest'
import { flushPromises } from '@vue/test-utils'
import PluginsView from '@/views/PluginsView.vue'
import PluginUploader from '@/components/plugins/PluginUploader.vue'
import { mountRouted } from '../helpers/mount'
import { stubRoutes as stubAllRoutes } from '../helpers/routes'
import { json, problem } from '../helpers/fetch'
import { PluginErrorCode } from '@/types/plugin'
import { PluginSettingKey } from '@/types/setting'

/** One installed plugin as the list endpoint returns it. */
const plugin = {
  id: 'example-pay',
  name: 'Example Pay',
  version: '1.2.0',
  author: 'Hostpanel',
  description: 'Card payments',
  type: 'payment',
  category: 'Payments',
  color: '#123456',
  iconUrl: null,
  isLoaded: true,
  isBuiltIn: false,
  status: 'Loaded',
  refusedRule: null,
  refusedDetail: null,
  stagedVersion: null,
  capabilities: [],
}

/**
 * The answer of a server whose plugin registry is switched off.
 *
 * @returns The 404 problem body carrying the registry-disabled code.
 */
const registryDisabled = (): Response =>
  json({ type: PluginErrorCode.RegistryDisabled, title: 'Disabled', status: 404, detail: 'The registry is off.' }, 404)

/** What the server reads out of an uploaded package, inside this server's version range. */
const preview = {
  id: 'example-pay',
  name: 'Example Pay',
  version: '1.3.0',
  author: 'Hostpanel',
  description: 'Card payments',
  hostAccess: [],
  capabilities: ['storedCredentials'],
  changelog: 'Faster checkout.',
  minHostVersion: null,
  maxHostVersion: null,
  hostVersion: '2.0.0',
  compatible: true,
}

/** One registry listing: the installed plugin with a newer version on offer. */
const listing = {
  id: 'example-pay',
  name: 'Example Pay',
  author: 'Hostpanel',
  description: 'Card payments',
  iconUrl: null,
  category: 'Payment',
  latestVersion: '2.0.0',
  sdkVersion: '3.0.0',
  verified: true,
  pricing: 'Free',
  installed: true,
  installedVersion: '1.2.0',
  updateAvailable: true,
}

/**
 * Stubs the routes a test names plus an empty slot widget list for the plugin: routes match by prefix, so
 * without it the widget request would be answered by the plugin list route.
 *
 * @param routes - Answers keyed by the start of `METHOD url`.
 * @returns The fetch mock, for asserting the requests made.
 */
const stubRoutes = (routes: Record<string, () => Response>) =>
  stubAllRoutes(Object.fromEntries(
    // The first key a request line starts with wins, so the longest (most specific) key goes first: a plain
    // override of the registry default would keep its early place and swallow `registry/updates`.
    Object.entries({
      'GET /api/admin/plugins/example-pay/slot-widgets': () => json([]),
      'GET /api/admin/plugins/registry': registryDisabled,
      ...routes,
    }).sort(([a], [b]) => b.length - a.length),
  ))

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

    expect(wrapper.get('[data-testid="plugins-list"]').text()).toContain('Example Pay')
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

    expect(wrapper.get('[data-testid="plugins-list"]').text()).toContain('Example Pay')
  })

  it('removes a plugin and reloads the list', async () => {
    const mock = stubRoutes({
      'GET /api/admin/plugins': () => json([plugin]),
      'DELETE /api/admin/plugins/example-pay': () => json({ requiresRestart: true }),
    })
    const wrapper = await mountPlugins()

    await wrapper.get('[data-testid="plugin-remove-example-pay"]').trigger('click')
    await flushPromises()
    expect(mock.mock.calls.some(([, init]) => init?.method === 'DELETE')).toBe(false)
    await wrapper.get('[data-testid="dialog-confirm"]').trigger('click')
    await flushPromises()

    expect(mock.mock.calls.some(([url, init]) => String(url) === '/api/admin/plugins/example-pay' && init?.method === 'DELETE')).toBe(true)
    expect(wrapper.get('[data-testid="plugins-feedback"]').text()).toContain('Plugin removed.')
  })

  it('shows the API sentence when the removal is refused', async () => {
    stubRoutes({
      'GET /api/admin/plugins': () => json([plugin]),
      'DELETE /api/admin/plugins/example-pay': () => problem('Plugin is in use.', 409),
    })
    const wrapper = await mountPlugins()

    await wrapper.get('[data-testid="plugin-remove-example-pay"]').trigger('click')
    await wrapper.get('[data-testid="dialog-confirm"]').trigger('click')
    await flushPromises()

    expect(wrapper.get('[data-testid="plugins-feedback"]').text()).toContain('Plugin is in use.')
    expect(wrapper.get('[data-testid="plugins-list"]').text()).toContain('Example Pay')
  })

  it('lists a refused plugin with its reason and offers no enable action', async () => {
    stubRoutes({
      'GET /api/admin/plugins': () => json([
        { ...plugin, isLoaded: false, status: 'Refused', refusedRule: 'LabelKeyMissing', refusedDetail: 'menu.title' },
      ]),
    })
    const wrapper = await mountPlugins()

    const card = wrapper.get('[data-testid="plugin-card-example-pay"]')
    expect(card.text()).toContain('Refused')
    expect(card.text()).toContain('A text key used by the plugin is missing from its English texts.')
    expect(card.text()).not.toContain('Active')
    expect(wrapper.find('[data-testid="plugin-remove-example-pay"]').exists()).toBe(true)
  })

  it('says in the removal prompt that the plugin data is kept', async () => {
    stubRoutes({ 'GET /api/admin/plugins': () => json([plugin]) })
    const wrapper = await mountPlugins()

    await wrapper.get('[data-testid="plugin-remove-example-pay"]').trigger('click')
    await flushPromises()

    expect(wrapper.text()).toContain('Its settings and stored data are kept')
  })

  it('purges the kept data of a removed plugin behind a confirmation', async () => {
    // The list follows the server: installed, then removed with its data kept, then gone after the purge.
    let rows: unknown[] = [plugin]
    const mock = stubRoutes({
      'GET /api/admin/plugins': () => json(rows),
      'DELETE /api/admin/plugins/example-pay/data': () => {
        rows = []
        return json({ requiresRestart: false })
      },
      'DELETE /api/admin/plugins/example-pay': () => {
        rows = [{ ...plugin, version: '', isLoaded: false, status: 'Removed' }]
        return json({ requiresRestart: true })
      },
    })
    const wrapper = await mountPlugins()
    await wrapper.get('[data-testid="plugin-remove-example-pay"]').trigger('click')
    await wrapper.get('[data-testid="dialog-confirm"]').trigger('click')
    await flushPromises()

    await wrapper.get('[data-testid="plugin-purge-example-pay"]').trigger('click')
    await flushPromises()
    /** Recorded requests to the purge endpoint of the removed plugin. */
    const purgeCalls = () => mock.mock.calls.filter(([url]) => String(url) === '/api/admin/plugins/example-pay/data')
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
      'POST /api/admin/plugins/inspect': () => json(preview),
      'POST /api/admin/plugins/install': () => json({ requiresRestart: true, stagedVersion: '1.3.0' }),
    })
    const wrapper = await mountPlugins()

    wrapper.getComponent(PluginUploader).vm.$emit('upload', new File(['z'], 'new.zip'))
    await flushPromises()
    await wrapper.get('[data-testid="plugin-install-confirm"]').trigger('click')
    await flushPromises()

    expect(wrapper.get('[data-testid="plugins-feedback"]').text()).toContain('Version 1.3.0 uploaded and staged.')
    expect(wrapper.find('[data-testid="plugins-restart-banner"]').exists()).toBe(true)
  })

  it('shows what an upload asks for and installs nothing until the admin confirms', async () => {
    const mock = stubRoutes({
      'GET /api/admin/plugins': () => json([plugin]),
      'POST /api/admin/plugins/inspect': () => json(preview),
      'POST /api/admin/plugins/install': () => json({ requiresRestart: true }),
    })
    const wrapper = await mountPlugins()

    wrapper.getComponent(PluginUploader).vm.$emit('upload', new File(['z'], 'new.zip'))
    await flushPromises()

    expect(wrapper.get('[data-testid="plugin-install-capability-storedCredentials"]').text()).toContain('saved cards')
    expect(wrapper.get('[data-testid="plugin-install-changelog"]').text()).toBe('Faster checkout.')
    expect(mock.mock.calls.some(([url]) => String(url) === '/api/admin/plugins/install')).toBe(false)

    await wrapper.get('[data-testid="plugin-install-cancel"]').trigger('click')
    await flushPromises()

    expect(wrapper.find('[data-testid="plugin-install-dialog"]').exists()).toBe(false)
    expect(mock.mock.calls.some(([url]) => String(url) === '/api/admin/plugins/install')).toBe(false)
  })

  it('shows the API sentence when the upload cannot be read', async () => {
    stubRoutes({
      'GET /api/admin/plugins': () => json([plugin]),
      'POST /api/admin/plugins/inspect': () => problem('Not a plugin archive.', 400),
    })
    const wrapper = await mountPlugins()

    wrapper.getComponent(PluginUploader).vm.$emit('upload', new File(['z'], 'bad.zip'))
    await flushPromises()

    expect(wrapper.get('[data-testid="plugins-feedback"]').text()).toContain('Not a plugin archive.')
    expect(wrapper.find('[data-testid="plugin-install-dialog"]').exists()).toBe(false)
  })

  it('shows the downgrade refusal and forces the older version after a confirmation', async () => {
    const mock = stubRoutes({
      'GET /api/admin/plugins': () => json([plugin]),
      'POST /api/admin/plugins/install?force=true': () => json({ requiresRestart: true }),
      'POST /api/admin/plugins/install': () => json(
        { type: 'PLUGIN_DOWNGRADE_REFUSED', title: 'Refused', status: 409, detail: 'Version 1.0.0 is older than 1.2.0.' },
        409,
      ),
      'POST /api/admin/plugins/inspect': () => json({ ...preview, version: '1.0.0' }),
    })
    const wrapper = await mountPlugins()

    wrapper.getComponent(PluginUploader).vm.$emit('upload', new File(['z'], 'old.zip'))
    await flushPromises()
    await wrapper.get('[data-testid="plugin-install-confirm"]').trigger('click')
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

    await wrapper.get('[data-testid="plugin-remove-example-pay"]').trigger('click')
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
      'PUT /api/admin/plugins/example-pay/slot-widgets/admin.service.panels/usage': () => json({ ...widget }),
      'GET /api/admin/plugins/example-pay/slot-widgets': () => json([widget]),
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
      'GET /api/admin/plugins/example-pay/slot-widgets': () => problem('Widgets are unavailable.', 503),
      'GET /api/admin/plugins': () => json([plugin]),
      'GET /api/admin/settings': () => json([signingRow]),
    })
    const wrapper = await mountPlugins()

    expect(wrapper.get('[data-testid="plugin-slot-widgets-error"]').text()).toContain('Widgets are unavailable.')
  })

  it('hides the Browse tab when the registry is switched off', async () => {
    stubRoutes({ 'GET /api/admin/plugins': () => json([plugin]) })
    const wrapper = await mountPlugins()

    expect(wrapper.find('[data-testid="plugins-tabs"]').exists()).toBe(false)
    expect(wrapper.find('[data-testid="plugins-tab-browse"]').exists()).toBe(false)
    expect(wrapper.find('[data-testid="plugins-list"]').exists()).toBe(true)
  })

  it('offers the Browse tab and lists the registry, with the trust card, when the registry is on', async () => {
    stubRoutes({
      'GET /api/admin/plugins/registry/updates': () => json([]),
      'GET /api/admin/plugins/registry': () => json({ items: [{ ...listing, installed: false, updateAvailable: false }], total: 1, pageSize: 10 }),
      'GET /api/admin/plugins': () => json([plugin]),
      'GET /api/admin/settings': () => json([signingRow, { id: 11, key: PluginSettingKey.RegistryTrust, value: 'VerifiedOnly' }]),
    })
    const wrapper = await mountPlugins()

    await wrapper.get('[data-testid="plugins-tab-browse"]').trigger('click')

    expect(wrapper.get('[data-testid="registry-card-example-pay"]').text()).toContain('Example Pay')
    expect(wrapper.find('[data-testid="plugin-registry-trust"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="plugins-list"]').exists()).toBe(false)
  })

  it('installs from the registry, shows the restart banner and the staged sentence', async () => {
    const mock = stubRoutes({
      'POST /api/admin/plugins/registry/example-pay/install': () => json({ requiresRestart: true, stagedVersion: '2.0.0' }),
      'GET /api/admin/plugins/registry/updates': () => json([listing]),
      'GET /api/admin/plugins/registry': () => json({ items: [listing], total: 1, pageSize: 10 }),
      'GET /api/admin/plugins': () => json([plugin]),
      'GET /api/admin/settings': () => json([signingRow]),
    })
    const wrapper = await mountPlugins()

    await wrapper.get('[data-testid="plugins-tab-browse"]').trigger('click')
    await wrapper.get('[data-testid="registry-install-example-pay"]').trigger('click')
    await flushPromises()

    const post = mock.mock.calls.find(([, init]) => init?.method === 'POST')
    expect(JSON.parse(String(post?.[1]?.body))).toEqual({ version: '2.0.0', allowDowngrade: false })
    expect(wrapper.find('[data-testid="plugins-restart-banner"]').exists()).toBe(true)
    expect(wrapper.get('[data-testid="plugins-feedback"]').text()).toContain('Version 2.0.0 uploaded and staged')
  })

  it('shows the API sentence when the registry install is refused', async () => {
    stubRoutes({
      'POST /api/admin/plugins/registry/example-pay/install': () => problem('That publisher is not trusted.', 403),
      'GET /api/admin/plugins/registry/updates': () => json([listing]),
      'GET /api/admin/plugins/registry': () => json({ items: [listing], total: 1, pageSize: 10 }),
      'GET /api/admin/plugins': () => json([plugin]),
      'GET /api/admin/settings': () => json([signingRow]),
    })
    const wrapper = await mountPlugins()

    await wrapper.get('[data-testid="plugins-tab-browse"]').trigger('click')
    await wrapper.get('[data-testid="registry-install-example-pay"]').trigger('click')
    await flushPromises()

    expect(wrapper.get('[data-testid="plugins-feedback"]').text()).toContain('That publisher is not trusted.')
    expect(wrapper.find('[data-testid="plugins-restart-banner"]').exists()).toBe(false)
  })

  it('shows an update badge on an installed plugin the registry has a newer version of', async () => {
    stubRoutes({
      'GET /api/admin/plugins/registry/updates': () => json([listing]),
      'GET /api/admin/plugins/registry': () => json({ items: [listing], total: 1, pageSize: 10 }),
      'GET /api/admin/plugins': () => json([plugin]),
      'GET /api/admin/settings': () => json([signingRow]),
    })
    const wrapper = await mountPlugins()

    expect(wrapper.get('[data-testid="plugin-update-example-pay"]').text()).toContain('2.0.0')
  })

  it('opens the details page from a registry card and from the installed list', async () => {
    stubRoutes({
      'GET /api/admin/plugins/registry/updates': () => json([listing]),
      'GET /api/admin/plugins/registry': () => json({ items: [listing], total: 1, pageSize: 10 }),
      'GET /api/admin/plugins': () => json([plugin]),
      'GET /api/admin/settings': () => json([signingRow]),
    })
    const { wrapper, router } = await mountRouted(PluginsView, '/plugins', '/plugins')
    await flushPromises()

    await wrapper.get('[data-testid="plugin-details-example-pay"]').trigger('click')
    await flushPromises()
    expect(router.currentRoute.value.path).toBe('/plugins/example-pay')

    await router.push('/plugins')
    await wrapper.get('[data-testid="plugins-tab-browse"]').trigger('click')
    await wrapper.get('[data-testid="registry-open-example-pay"]').trigger('click')
    await flushPromises()
    expect(router.currentRoute.value.path).toBe('/plugins/example-pay')
  })

  it('updates an installed plugin from its Update button', async () => {
    const mock = stubRoutes({
      'POST /api/admin/plugins/registry/example-pay/install': () => json({ requiresRestart: true, stagedVersion: '2.0.0' }),
      'GET /api/admin/plugins/registry/updates': () => json([listing]),
      'GET /api/admin/plugins/registry': () => json({ items: [listing], total: 1, pageSize: 10 }),
      'GET /api/admin/plugins': () => json([plugin]),
      'GET /api/admin/settings': () => json([signingRow]),
    })
    const wrapper = await mountPlugins()

    await wrapper.get('[data-testid="plugin-update-button-example-pay"]').trigger('click')
    await flushPromises()

    const post = mock.mock.calls.find(([, init]) => init?.method === 'POST')
    expect(JSON.parse(String(post?.[1]?.body))).toEqual({ version: '2.0.0', allowDowngrade: false })
    expect(wrapper.get('[data-testid="plugins-feedback"]').text()).toContain('Version 2.0.0 uploaded and staged')
  })

  it('uninstalls from a registry card behind the same removal confirmation', async () => {
    const mock = stubRoutes({
      'DELETE /api/admin/plugins/example-pay': () => json({ requiresRestart: true }),
      'GET /api/admin/plugins/registry/updates': () => json([]),
      'GET /api/admin/plugins/registry': () => json({ items: [{ ...listing, updateAvailable: false }], total: 1, pageSize: 10 }),
      'GET /api/admin/plugins': () => json([plugin]),
      'GET /api/admin/settings': () => json([signingRow]),
    })
    const wrapper = await mountPlugins()

    await wrapper.get('[data-testid="plugins-tab-browse"]').trigger('click')
    await wrapper.get('[data-testid="registry-uninstall-example-pay"]').trigger('click')
    await flushPromises()
    expect(mock.mock.calls.some(([, init]) => init?.method === 'DELETE')).toBe(false)
    await wrapper.get('[data-testid="dialog-confirm"]').trigger('click')
    await flushPromises()

    expect(mock.mock.calls.some(([url, init]) => String(url) === '/api/admin/plugins/example-pay' && init?.method === 'DELETE')).toBe(true)
    expect(wrapper.get('[data-testid="plugins-feedback"]').text()).toContain('Plugin removed.')
  })
})

describe('PluginsView registration card', () => {
  afterEach(() => {
    vi.unstubAllGlobals()
  })

  /** A registration that failed, as the server answers it. */
  const failedInstance = {
    state: 'Error',
    instanceId: null,
    baseUrl: null,
    errorCode: 'PLUGIN_REGISTRY_INSTANCE_ADDRESS_INVALID',
    message: 'The plugin registry cannot use the public address of this Hostpanel.',
    verifiedAt: null,
    nextAttemptAt: null,
  }

  it('shows the registration state on the Browse tab, with the sentence of the last failure', async () => {
    stubRoutes({
      'GET /api/admin/plugins/registry-instance': () => json(failedInstance),
      'GET /api/admin/plugins/registry/updates': () => json([]),
      'GET /api/admin/plugins/registry': () => json({ items: [listing], total: 1, pageSize: 10 }),
      'GET /api/admin/plugins': () => json([plugin]),
      'GET /api/admin/settings': () => json([signingRow]),
    })
    const wrapper = await mountPlugins()

    await wrapper.get('[data-testid="plugins-tab-browse"]').trigger('click')

    expect(wrapper.get('[data-testid="plugin-registry-instance-state"]').text()).toBe('Failed')
    expect(wrapper.get('[data-testid="plugin-registry-instance-problem"]').text()).toContain('cannot use the public address')
  })

  it('retries the registration from the card and shows the state the server answers', async () => {
    const mock = stubRoutes({
      'POST /api/admin/plugins/registry-instance/retry': () => json({ ...failedInstance, state: 'Verified', errorCode: null, message: null, verifiedAt: '2026-11-08T12:00:00Z' }),
      'GET /api/admin/plugins/registry-instance': () => json(failedInstance),
      'GET /api/admin/plugins/registry/updates': () => json([]),
      'GET /api/admin/plugins/registry': () => json({ items: [listing], total: 1, pageSize: 10 }),
      'GET /api/admin/plugins': () => json([plugin]),
      'GET /api/admin/settings': () => json([signingRow]),
    })
    const wrapper = await mountPlugins()
    await wrapper.get('[data-testid="plugins-tab-browse"]').trigger('click')

    await wrapper.get('[data-testid="plugin-registry-instance-retry"]').trigger('click')
    await flushPromises()

    expect(mock.mock.calls.some(([url, init]) => String(url) === '/api/admin/plugins/registry-instance/retry' && init?.method === 'POST')).toBe(true)
    expect(wrapper.get('[data-testid="plugin-registry-instance-state"]').text()).toBe('Verified')
    expect(wrapper.find('[data-testid="plugin-registry-instance-retry"]').exists()).toBe(false)
  })

  it('asks for no registration state while the registry is switched off', async () => {
    const mock = stubRoutes({ 'GET /api/admin/plugins': () => json([plugin]) })
    await mountPlugins()

    expect(mock.mock.calls.some(([url]) => String(url).includes('registry-instance'))).toBe(false)
  })
})
