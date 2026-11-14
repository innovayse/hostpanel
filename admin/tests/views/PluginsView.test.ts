/**
 * Mount tests for PluginsView: the installed list, the empty state, the restart banner after an
 * install, a refusal shown as the API's sentence, the retry of a refused load and the danger
 * Settings tab: the "Allow unsigned plugins" switch (shown from the stored setting, saved through the settings
 * endpoint, a refusal shown as the API's sentence, a failed load with its retry) and the purge of kept data.
 * The Upload plugin dialog, the tab kept in the address, the Browse tab (hidden when the registry is switched
 * off, shown otherwise, with the registry list, an install from it, the restart banner it raises) and the update
 * button of an installed plugin.
 */
import { afterEach, describe, expect, it, vi } from 'vitest'
import { flushPromises } from '@vue/test-utils'
import PluginsView from '@/views/PluginsView.vue'
import PluginUploader from '@/components/plugins/PluginUploader.vue'
import { mountRouted } from '../helpers/mount'
import { stubRoutes as stubAllRoutes } from '../helpers/routes'
import { json, problem } from '../helpers/fetch'
import { PluginErrorCode } from '@/types/plugin'
import { makeDetails, makeVersion } from '../helpers/pluginDetails'
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

/** The registry details of the updatable plugin: installed 1.2.0, 2.0.0 on offer with notes, 1.3.0 without. */
const updateDetails = makeDetails({
  installedVersion: '1.2.0',
  latestVersion: '2.0.0',
  versions: [
    makeVersion({ version: '2.0.0', changelog: 'New **checkout** flow.' }),
    makeVersion({ version: '1.3.0' }),
    makeVersion({ version: '1.2.0', changelog: 'Installed notes.' }),
  ],
})

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

/**
 * Opens a tab from its strip and waits for the address change it makes.
 *
 * @param wrapper - The mounted screen.
 * @param tab - The tab's value, as in its test id.
 */
const openTab = async (wrapper: Awaited<ReturnType<typeof mountPlugins>>, tab: 'browse' | 'settings'): Promise<void> => {
  await wrapper.get(`[data-testid="plugins-tab-${tab}"]`).trigger('click')
  await flushPromises()
}

/**
 * Uploads an archive through the header's dialog.
 *
 * @param wrapper - The mounted screen.
 * @param file - The archive the uploader reports.
 */
const upload = async (wrapper: Awaited<ReturnType<typeof mountPlugins>>, file: File): Promise<void> => {
  await wrapper.get('[data-testid="plugins-upload-open"]').trigger('click')
  await flushPromises()
  wrapper.getComponent(PluginUploader).vm.$emit('upload', file)
  await flushPromises()
}

describe('PluginsView', () => {
  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('draws card placeholders while the list loads', async () => {
    vi.stubGlobal('fetch', vi.fn(() => new Promise<Response>(() => undefined)))
    const wrapper = await mountPlugins()

    expect(wrapper.get('[data-testid="plugins-loading"]').attributes('aria-busy')).toBe('true')
    expect(wrapper.findAll('[data-testid="plugin-card-skeleton"]')).toHaveLength(6)
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

  it('lists a refused plugin with its reason and offers no update action', async () => {
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
    expect(wrapper.find('[data-testid="plugin-update-button-example-pay"]').exists()).toBe(false)
  })

  it('purges the kept data of a removed plugin behind a confirmation', async () => {
    // The list follows the server: a removed plugin with its data kept, then gone after the purge.
    let rows: unknown[] = [{ ...plugin, version: '', isLoaded: false, status: 'Removed' }]
    const mock = stubRoutes({
      'GET /api/admin/plugins': () => json(rows),
      'DELETE /api/admin/plugins/example-pay/data': () => {
        rows = []
        return json({ requiresRestart: false })
      },
    })
    const wrapper = await mountPlugins()
    await openTab(wrapper, 'settings')

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

  it('keeps the data when the purge confirmation is dismissed', async () => {
    const mock = stubRoutes({
      'GET /api/admin/plugins': () => json([{ ...plugin, version: '', isLoaded: false, status: 'Removed' }]),
    })
    const wrapper = await mountPlugins()
    await openTab(wrapper, 'settings')

    await wrapper.get('[data-testid="plugin-purge-example-pay"]').trigger('click')
    await wrapper.get('[data-testid="dialog-cancel"]').trigger('click')
    await flushPromises()

    expect(mock.mock.calls.some(([, init]) => init?.method === 'DELETE')).toBe(false)
    expect(wrapper.find('[data-testid="plugins-purge-dialog"]').exists()).toBe(false)
  })

  it('keeps the tab in the address and opens it from there', async () => {
    stubRoutes({ 'GET /api/admin/plugins': () => json([plugin]), 'GET /api/admin/settings': () => json([signingRow]) })
    const { wrapper, router } = await mountRouted(PluginsView, '/plugins', '/plugins')
    await flushPromises()

    await openTab(wrapper, 'settings')
    expect(router.currentRoute.value.query.tab).toBe('settings')
    expect(wrapper.find('[data-testid="plugin-signing"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="plugins-list"]').exists()).toBe(false)

    const reopened = await mountRouted(PluginsView, '/plugins', '/plugins?tab=settings')
    await flushPromises()
    expect(reopened.wrapper.find('[data-testid="plugin-signing"]').exists()).toBe(true)
  })

  it('ends the install with the restart note and a Close button', async () => {
    stubRoutes({
      'GET /api/admin/plugins': () => json([plugin]),
      'POST /api/admin/plugins/inspect': () => json(preview),
      'POST /api/admin/plugins/install': () => json({ requiresRestart: true }),
    })
    const wrapper = await mountPlugins()
    await upload(wrapper, new File(['z'], 'new.zip'))
    await wrapper.get('[data-testid="plugin-install-confirm"]').trigger('click')
    await flushPromises()

    expect(wrapper.find('[data-testid="plugin-processing-restart"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="plugin-processing-running"]').exists()).toBe(false)
    await wrapper.get('[data-testid="plugin-processing-close"]').trigger('click')
    expect(wrapper.find('[data-testid="plugin-processing-dialog"]').exists()).toBe(false)
  })

  it('shows the running state without a close button until the install ends', async () => {
    vi.stubGlobal('fetch', vi.fn((url: string) => (String(url).endsWith('/inspect') ? Promise.resolve(json(preview)) : new Promise<Response>(() => undefined))))
    const wrapper = await mountPlugins()
    await upload(wrapper, new File(['z'], 'new.zip'))
    await wrapper.get('[data-testid="plugin-install-confirm"]').trigger('click')
    await flushPromises()

    expect(wrapper.find('[data-testid="plugin-processing-running"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="dialog-close"]').exists()).toBe(false)
    expect(wrapper.find('[data-testid="plugin-processing-close"]').exists()).toBe(false)
  })

  it('opens the upload dialog from the header and closes it once an archive is chosen', async () => {
    stubRoutes({ 'GET /api/admin/plugins': () => json([plugin]), 'POST /api/admin/plugins/inspect': () => json(preview) })
    const wrapper = await mountPlugins()
    expect(wrapper.find('[data-testid="plugins-upload-dialog"]').exists()).toBe(false)

    await wrapper.get('[data-testid="plugins-upload-open"]').trigger('click')
    await flushPromises()
    expect(wrapper.find('[data-testid="plugins-upload-dialog"]').exists()).toBe(true)

    wrapper.getComponent(PluginUploader).vm.$emit('upload', new File(['z'], 'new.zip'))
    await flushPromises()
    expect(wrapper.find('[data-testid="plugins-upload-dialog"]').exists()).toBe(false)
  })

  it('says a newer upload is staged and shows the restart banner', async () => {
    stubRoutes({
      'GET /api/admin/plugins': () => json([plugin]),
      'POST /api/admin/plugins/inspect': () => json(preview),
      'POST /api/admin/plugins/install': () => json({ requiresRestart: true, stagedVersion: '1.3.0' }),
    })
    const wrapper = await mountPlugins()

    await upload(wrapper, new File(['z'], 'new.zip'))
    await wrapper.get('[data-testid="plugin-install-confirm"]').trigger('click')
    await flushPromises()

    expect(wrapper.get('[data-testid="plugin-processing-message"]').text()).toContain('Version 1.3.0 uploaded and staged.')
    expect(wrapper.find('[data-testid="plugins-restart-banner"]').exists()).toBe(true)
  })

  it('shows what an upload asks for and installs nothing until the admin confirms', async () => {
    const mock = stubRoutes({
      'GET /api/admin/plugins': () => json([plugin]),
      'POST /api/admin/plugins/inspect': () => json(preview),
      'POST /api/admin/plugins/install': () => json({ requiresRestart: true }),
    })
    const wrapper = await mountPlugins()

    await upload(wrapper, new File(['z'], 'new.zip'))

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

    await upload(wrapper, new File(['z'], 'bad.zip'))

    expect(wrapper.get('[data-testid="plugin-processing-message"]').text()).toContain('Not a plugin archive.')
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

    await upload(wrapper, new File(['z'], 'old.zip'))
    await wrapper.get('[data-testid="plugin-install-confirm"]').trigger('click')
    await flushPromises()

    expect(wrapper.get('[data-testid="plugins-downgrade-dialog"]').text()).toContain('Version 1.0.0 is older than 1.2.0.')
    expect(wrapper.find('[data-testid="plugin-processing-dialog"]').exists()).toBe(false)
    expect(wrapper.find('[data-testid="plugins-restart-banner"]').exists()).toBe(false)
    await wrapper.get('[data-testid="dialog-confirm"]').trigger('click')
    await flushPromises()

    expect(mock.mock.calls.some(([url]) => String(url) === '/api/admin/plugins/install?force=true')).toBe(true)
    expect(wrapper.find('[data-testid="plugins-restart-banner"]').exists()).toBe(true)
  })

  it('shows the signing switch from the stored setting, with its warning', async () => {
    stubRoutes({
      'GET /api/admin/plugins': () => json([plugin]),
      'GET /api/admin/settings': () => json([signingRow]),
    })
    const wrapper = await mountPlugins()
    await openTab(wrapper, 'settings')

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
    await openTab(wrapper, 'settings')

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
    await openTab(wrapper, 'settings')

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
    await openTab(wrapper, 'settings')

    expect(wrapper.get('[data-testid="plugin-signing-error"]').text()).toContain('Settings are unavailable.')
    expect(wrapper.find('[data-testid="plugin-allow-unsigned"]').exists()).toBe(false)

    await wrapper.get('[data-testid="plugin-signing-error-retry"]').trigger('click')
    await flushPromises()

    expect(wrapper.find('[data-testid="plugin-allow-unsigned"]').exists()).toBe(true)
  })

  it('hides the Browse tab when the registry is switched off', async () => {
    stubRoutes({ 'GET /api/admin/plugins': () => json([plugin]) })
    const wrapper = await mountPlugins()

    expect(wrapper.find('[data-testid="plugins-tab-settings"]').exists()).toBe(true)
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

    await openTab(wrapper, 'browse')

    expect(wrapper.get('[data-testid="plugin-card-example-pay"]').text()).toContain('Example Pay')
    expect(wrapper.find('[data-testid="plugin-registry-trust"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="plugins-list"]').exists()).toBe(false)
  })

  it('installs from the registry, shows the restart banner and the staged sentence', async () => {
    const mock = stubRoutes({
      'GET /api/admin/plugins/registry/example-pay': () => json(updateDetails),
      'POST /api/admin/plugins/registry/example-pay/install': () => json({ requiresRestart: true, stagedVersion: '2.0.0' }),
      'GET /api/admin/plugins/registry/updates': () => json([listing]),
      'GET /api/admin/plugins/registry': () => json({ items: [listing], total: 1, pageSize: 10 }),
      'GET /api/admin/plugins': () => json([plugin]),
      'GET /api/admin/settings': () => json([signingRow]),
    })
    const wrapper = await mountPlugins()

    await openTab(wrapper, 'browse')
    await wrapper.get('[data-testid="registry-install-example-pay"]').trigger('click')
    await flushPromises()
    // An update of an installed plugin waits for the confirmation.
    expect(mock.mock.calls.some(([, init]) => init?.method === 'POST')).toBe(false)
    await wrapper.get('[data-testid="dialog-confirm"]').trigger('click')
    await flushPromises()

    const post = mock.mock.calls.find(([, init]) => init?.method === 'POST')
    expect(JSON.parse(String(post?.[1]?.body))).toEqual({ version: '2.0.0', allowDowngrade: false })
    expect(wrapper.find('[data-testid="plugins-restart-banner"]').exists()).toBe(true)
    expect(wrapper.get('[data-testid="plugin-processing-message"]').text()).toContain('Version 2.0.0 uploaded and staged')
  })

  it('shows the API sentence when the registry install is refused', async () => {
    stubRoutes({
      'GET /api/admin/plugins/registry/example-pay': () => json(updateDetails),
      'POST /api/admin/plugins/registry/example-pay/install': () => problem('That publisher is not trusted.', 403),
      'GET /api/admin/plugins/registry/updates': () => json([listing]),
      'GET /api/admin/plugins/registry': () => json({ items: [listing], total: 1, pageSize: 10 }),
      'GET /api/admin/plugins': () => json([plugin]),
      'GET /api/admin/settings': () => json([signingRow]),
    })
    const wrapper = await mountPlugins()

    await openTab(wrapper, 'browse')
    await wrapper.get('[data-testid="registry-install-example-pay"]').trigger('click')
    await flushPromises()
    await wrapper.get('[data-testid="dialog-confirm"]').trigger('click')
    await flushPromises()

    expect(wrapper.get('[data-testid="plugin-processing-message"]').text()).toContain('That publisher is not trusted.')
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

    expect(wrapper.find('[data-testid="plugin-card-badge-update-example-pay"]').exists()).toBe(true)
    expect(wrapper.get('[data-testid="plugin-update-button-example-pay"]').text()).toContain('2.0.0')
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

    await wrapper.get('[data-testid="plugin-card-link-example-pay"]').trigger('click')
    await flushPromises()
    expect(router.currentRoute.value.path).toBe('/plugins/example-pay')

    await router.push('/plugins')
    await openTab(wrapper, 'browse')
    await wrapper.get('[data-testid="registry-settings-example-pay"]').trigger('click')
    await flushPromises()
    expect(router.currentRoute.value.path).toBe('/plugins/example-pay')
  })

  it('lists what the update changes before it installs, and installs nothing when dismissed', async () => {
    const mock = stubRoutes({
      'GET /api/admin/plugins/registry/example-pay': () => json(updateDetails),
      'POST /api/admin/plugins/registry/example-pay/install': () => json({ requiresRestart: true }),
      'GET /api/admin/plugins/registry/updates': () => json([listing]),
      'GET /api/admin/plugins/registry': () => json({ items: [listing], total: 1, pageSize: 10 }),
      'GET /api/admin/plugins': () => json([plugin]),
      'GET /api/admin/settings': () => json([signingRow]),
    })
    const wrapper = await mountPlugins()

    await wrapper.get('[data-testid="plugin-update-button-example-pay"]').trigger('click')
    await flushPromises()

    const dialog = wrapper.get('[data-testid="plugins-update-dialog"]')
    expect(dialog.text()).toContain('Update example-pay to version 2.0.0')
    expect(dialog.get('[data-testid="plugin-update-changes-heading"]').text()).toBe('What changes from 1.2.0 to 2.0.0')
    expect(dialog.get('[data-testid="plugin-update-change-2.0.0"] strong').text()).toBe('checkout')
    expect(dialog.get('[data-testid="plugin-update-change-1.3.0"]').text()).toContain('No release notes.')
    expect(dialog.find('[data-testid="plugin-update-change-1.2.0"]').exists()).toBe(false)

    await wrapper.get('[data-testid="dialog-cancel"]').trigger('click')
    await flushPromises()

    expect(wrapper.find('[data-testid="plugins-update-dialog"]').exists()).toBe(false)
    expect(mock.mock.calls.some(([, init]) => init?.method === 'POST')).toBe(false)
  })

  it('still lets the admin confirm an update when the notes cannot be read', async () => {
    const mock = stubRoutes({
      'GET /api/admin/plugins/registry/example-pay': () => problem('The registry is unavailable.', 503),
      'POST /api/admin/plugins/registry/example-pay/install': () => json({ requiresRestart: true }),
      'GET /api/admin/plugins/registry/updates': () => json([listing]),
      'GET /api/admin/plugins/registry': () => json({ items: [listing], total: 1, pageSize: 10 }),
      'GET /api/admin/plugins': () => json([plugin]),
      'GET /api/admin/settings': () => json([signingRow]),
    })
    const wrapper = await mountPlugins()

    await wrapper.get('[data-testid="plugin-update-button-example-pay"]').trigger('click')
    await flushPromises()

    expect(wrapper.find('[data-testid="plugin-update-changes"]').exists()).toBe(false)
    await wrapper.get('[data-testid="dialog-confirm"]').trigger('click')
    await flushPromises()

    expect(mock.mock.calls.some(([, init]) => init?.method === 'POST')).toBe(true)
  })

  it('updates an installed plugin from its Update button, after the confirmation', async () => {
    const mock = stubRoutes({
      'GET /api/admin/plugins/registry/example-pay': () => json(updateDetails),
      'POST /api/admin/plugins/registry/example-pay/install': () => json({ requiresRestart: true, stagedVersion: '2.0.0' }),
      'GET /api/admin/plugins/registry/updates': () => json([listing]),
      'GET /api/admin/plugins/registry': () => json({ items: [listing], total: 1, pageSize: 10 }),
      'GET /api/admin/plugins': () => json([plugin]),
      'GET /api/admin/settings': () => json([signingRow]),
    })
    const wrapper = await mountPlugins()

    await wrapper.get('[data-testid="plugin-update-button-example-pay"]').trigger('click')
    await flushPromises()
    await wrapper.get('[data-testid="dialog-confirm"]').trigger('click')
    await flushPromises()

    const post = mock.mock.calls.find(([, init]) => init?.method === 'POST')
    expect(JSON.parse(String(post?.[1]?.body))).toEqual({ version: '2.0.0', allowDowngrade: false })
    expect(wrapper.get('[data-testid="plugin-processing-message"]').text()).toContain('Version 2.0.0 uploaded and staged')
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

    await openTab(wrapper, 'browse')

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
    await openTab(wrapper, 'browse')

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
  it('loads the registry with the search, category and page held in the address', async () => {
    const mock = stubRoutes({
      'GET /api/admin/plugins/registry/updates': () => json([]),
      'GET /api/admin/plugins/registry': () => json({ items: [listing], total: 30, pageSize: 10 }),
      'GET /api/admin/plugins': () => json([plugin]),
      'GET /api/admin/settings': () => json([signingRow]),
    })
    const { wrapper } = await mountRouted(PluginsView, '/plugins', '/plugins?search=pay&category=Payment&page=2')
    await flushPromises()
    await openTab(wrapper, 'browse')

    const request = mock.mock.calls.map(([url]) => String(url)).find(url => url.startsWith('/api/admin/plugins/registry?'))
    expect(request).toContain('q=pay')
    expect(request).toContain('category=Payment')
    expect(request).toContain('page=2')
    expect((wrapper.get('[data-testid="plugin-registry-search"]').element as HTMLInputElement).value).toBe('pay')
  })
  it('narrows the installed list by the search text and the status in the address', async () => {
    const mailer = { ...plugin, id: 'example-mail', name: 'Mailer', description: 'Sends email', isLoaded: false }
    stubRoutes({
      'GET /api/admin/plugins/example-mail/slot-widgets': () => json([]),
      'GET /api/admin/plugins': () => json([plugin, mailer]),
      'GET /api/admin/settings': () => json([signingRow]),
    })
    const { wrapper } = await mountRouted(PluginsView, '/plugins', '/plugins?installedSearch=mail&status=pendingRestart')
    await flushPromises()

    expect(wrapper.find('[data-testid="plugin-card-example-mail"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="plugin-card-example-pay"]').exists()).toBe(false)
  })

  it('shows the no-match state when no installed plugin fits the filters', async () => {
    stubRoutes({
      'GET /api/admin/plugins': () => json([plugin]),
      'GET /api/admin/settings': () => json([signingRow]),
    })
    const { wrapper } = await mountRouted(PluginsView, '/plugins', '/plugins?installedSearch=nothing-like-this')
    await flushPromises()

    expect(wrapper.find('[data-testid="plugins-no-match"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="plugin-card-example-pay"]').exists()).toBe(false)
  })
})
